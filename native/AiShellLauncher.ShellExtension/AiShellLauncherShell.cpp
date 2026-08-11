#include <windows.h>
#include <shlobj.h>
#include <shlwapi.h>
#include <shobjidl.h>
#include <servprov.h>

#include <new>
#include <string>
#include <vector>

#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "shlwapi.lib")
#pragma comment(linker, "/EXPORT:DllCanUnloadNow,PRIVATE")
#pragma comment(linker, "/EXPORT:DllGetClassObject,PRIVATE")

static constexpr DWORD SlotCount = 16;

// Slots use {4B7A4183-5B26-46C7-A8BF-01CFE8B13300} through ...330F.
static const CLSID CLSID_AiShellLauncherSlotBase =
{ 0x4b7a4183, 0x5b26, 0x46c7, { 0xa8, 0xbf, 0x01, 0xcf, 0xe8, 0xb1, 0x33, 0x00 } };

static HINSTANCE g_module = nullptr;
static long g_moduleReferences = 0;

struct MenuEntry
{
    std::wstring toolId;
    std::wstring modeId;
    std::wstring title;
    std::wstring risk;
    std::wstring iconPath;
};

static CLSID MakeSlotClsid(DWORD slot)
{
    CLSID value = CLSID_AiShellLauncherSlotBase;
    value.Data4[7] = static_cast<unsigned char>(slot);
    return value;
}

static bool TryGetSlotIndex(REFCLSID classId, DWORD* slot)
{
    if (!slot ||
        classId.Data1 != CLSID_AiShellLauncherSlotBase.Data1 ||
        classId.Data2 != CLSID_AiShellLauncherSlotBase.Data2 ||
        classId.Data3 != CLSID_AiShellLauncherSlotBase.Data3) {
        return false;
    }

    for (size_t index = 0; index < 7; ++index) {
        if (classId.Data4[index] != CLSID_AiShellLauncherSlotBase.Data4[index]) {
            return false;
        }
    }

    if (classId.Data4[7] >= SlotCount) {
        return false;
    }

    *slot = classId.Data4[7];
    return true;
}

static HRESULT DuplicateString(const std::wstring& value, PWSTR* output)
{
    if (!output) {
        return E_POINTER;
    }

    *output = nullptr;
    const size_t bytes = (value.size() + 1) * sizeof(wchar_t);
    auto copy = static_cast<PWSTR>(CoTaskMemAlloc(bytes));
    if (!copy) {
        return E_OUTOFMEMORY;
    }

    memcpy(copy, value.c_str(), bytes);
    *output = copy;
    return S_OK;
}

static std::wstring QuoteArgument(const std::wstring& value)
{
    std::wstring quoted = L"\"";
    size_t backslashes = 0;
    for (const wchar_t character : value) {
        if (character == L'\\') {
            ++backslashes;
            continue;
        }

        if (character == L'\"') {
            quoted.append(backslashes * 2 + 1, L'\\');
            quoted.push_back(L'\"');
            backslashes = 0;
            continue;
        }

        quoted.append(backslashes, L'\\');
        backslashes = 0;
        quoted.push_back(character);
    }

    quoted.append(backslashes * 2, L'\\');
    quoted.push_back(L'\"');
    return quoted;
}

static std::wstring GetLocalAppDataDirectory()
{
    PWSTR localAppData = nullptr;
    const HRESULT result = SHGetKnownFolderPath(FOLDERID_LocalAppData, KF_FLAG_DEFAULT, nullptr, &localAppData);
    if (FAILED(result) || !localAppData) {
        CoTaskMemFree(localAppData);
        return {};
    }

    std::wstring path(localAppData);
    CoTaskMemFree(localAppData);
    return path;
}

static std::wstring GetRuntimeDirectory()
{
    const std::wstring localAppData = GetLocalAppDataDirectory();
    return localAppData.empty() ? std::wstring() : localAppData + L"\\AiShellLauncher\\Runtime";
}

static std::wstring GetModuleDirectory()
{
    std::vector<wchar_t> buffer(32768);
    const DWORD length = GetModuleFileNameW(g_module, buffer.data(), static_cast<DWORD>(buffer.size()));
    if (length == 0 || length >= buffer.size()) {
        return {};
    }

    std::wstring path(buffer.data(), length);
    const size_t separator = path.find_last_of(L"\\/");
    return separator == std::wstring::npos ? std::wstring() : path.substr(0, separator);
}

static std::vector<std::wstring> Split(const std::wstring& value, wchar_t delimiter)
{
    std::vector<std::wstring> parts;
    size_t start = 0;
    while (start <= value.size()) {
        const size_t end = value.find(delimiter, start);
        if (end == std::wstring::npos) {
            parts.push_back(value.substr(start));
            break;
        }

        parts.push_back(value.substr(start, end - start));
        start = end + 1;
    }
    return parts;
}

static std::wstring GetMenuIndexPath()
{
    const std::wstring localAppData = GetLocalAppDataDirectory();
    return localAppData.empty() ? std::wstring() : localAppData + L"\\AiShellLauncher\\menu.tsv";
}

static std::wstring ReadUtf8File(const std::wstring& path)
{
    HANDLE file = CreateFileW(
        path.c_str(),
        GENERIC_READ,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        nullptr,
        OPEN_EXISTING,
        FILE_ATTRIBUTE_NORMAL,
        nullptr);
    if (file == INVALID_HANDLE_VALUE) {
        return {};
    }

    LARGE_INTEGER size{};
    if (!GetFileSizeEx(file, &size) || size.QuadPart <= 0 || size.QuadPart > 1024 * 1024) {
        CloseHandle(file);
        return {};
    }

    std::string bytes(static_cast<size_t>(size.QuadPart), '\0');
    DWORD bytesRead = 0;
    const BOOL read = ReadFile(file, bytes.data(), static_cast<DWORD>(bytes.size()), &bytesRead, nullptr);
    CloseHandle(file);
    if (!read || bytesRead != bytes.size()) {
        return {};
    }

    const int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, bytes.data(), static_cast<int>(bytes.size()), nullptr, 0);
    if (length <= 0) {
        return {};
    }

    std::wstring text(static_cast<size_t>(length), L'\0');
    if (!MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, bytes.data(), static_cast<int>(bytes.size()), text.data(), length)) {
        return {};
    }

    if (!text.empty() && text.front() == 0xfeff) {
        text.erase(text.begin());
    }
    return text;
}

static bool TryLoadMenuEntry(DWORD slot, MenuEntry* entry)
{
    if (!entry) {
        return false;
    }

    const std::wstring text = ReadUtf8File(GetMenuIndexPath());
    if (text.empty()) {
        return false;
    }

    for (const auto& rawLine : Split(text, L'\n')) {
        std::wstring line = rawLine;
        if (!line.empty() && line.back() == L'\r') {
            line.pop_back();
        }

        const auto fields = Split(line, L'\t');
        if (fields.size() < 7 || fields[0] != L"C") {
            continue;
        }

        wchar_t* end = nullptr;
        const unsigned long parsedSlot = wcstoul(fields[1].c_str(), &end, 10);
        if (!end || *end != L'\0' || parsedSlot != slot) {
            continue;
        }

        *entry = MenuEntry{ fields[2], fields[3], fields[4], fields[5], fields[6] };
        return true;
    }
    return false;
}

static HRESULT GetPathFromItems(IShellItemArray* items, PWSTR* path)
{
    if (!path) {
        return E_POINTER;
    }
    *path = nullptr;
    if (!items) {
        return E_INVALIDARG;
    }

    DWORD count = 0;
    HRESULT result = items->GetCount(&count);
    if (FAILED(result) || count == 0) {
        return E_FAIL;
    }

    IShellItem* item = nullptr;
    result = items->GetItemAt(0, &item);
    if (SUCCEEDED(result)) {
        result = item->GetDisplayName(SIGDN_FILESYSPATH, path);
        item->Release();
    }
    return result;
}

static HRESULT GetPathFromSite(IUnknown* site, PWSTR* path)
{
    if (!path) {
        return E_POINTER;
    }
    *path = nullptr;
    if (!site) {
        return E_NOINTERFACE;
    }

    IServiceProvider* provider = nullptr;
    HRESULT result = site->QueryInterface(IID_PPV_ARGS(&provider));
    if (FAILED(result)) {
        return result;
    }

    IFolderView* folderView = nullptr;
    result = provider->QueryService(SID_SFolderView, IID_PPV_ARGS(&folderView));
    provider->Release();
    if (FAILED(result)) {
        return result;
    }

    IShellItem* folder = nullptr;
    result = folderView->GetFolder(IID_PPV_ARGS(&folder));
    folderView->Release();
    if (SUCCEEDED(result)) {
        result = folder->GetDisplayName(SIGDN_FILESYSPATH, path);
        folder->Release();
    }
    return result;
}

class SlotExplorerCommand final : public IExplorerCommand, public IObjectWithSite
{
public:
    explicit SlotExplorerCommand(DWORD slot) : references_(1), site_(nullptr), slot_(slot)
    {
        InterlockedIncrement(&g_moduleReferences);
    }

    IFACEMETHODIMP QueryInterface(REFIID interfaceId, void** object) override
    {
        if (!object) {
            return E_POINTER;
        }
        *object = nullptr;
        if (interfaceId == IID_IUnknown || interfaceId == IID_IExplorerCommand) {
            *object = static_cast<IExplorerCommand*>(this);
        } else if (interfaceId == IID_IObjectWithSite) {
            *object = static_cast<IObjectWithSite*>(this);
        } else {
            return E_NOINTERFACE;
        }
        AddRef();
        return S_OK;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&references_);
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG references = InterlockedDecrement(&references_);
        if (references == 0) {
            delete this;
        }
        return references;
    }

    IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* title) override
    {
        MenuEntry entry;
        if (!TryLoadMenuEntry(slot_, &entry)) {
            return DuplicateString(L"", title);
        }
        return DuplicateString(entry.title, title);
    }

    IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
    {
        MenuEntry entry;
        if (TryLoadMenuEntry(slot_, &entry) && !entry.iconPath.empty()) {
            std::vector<wchar_t> expanded(32768);
            const DWORD length = ExpandEnvironmentStringsW(
                entry.iconPath.c_str(), expanded.data(), static_cast<DWORD>(expanded.size()));
            const std::wstring customIcon = length > 0 && length < expanded.size()
                ? std::wstring(expanded.data(), length - 1)
                : entry.iconPath;
            if (PathFileExistsW(customIcon.c_str())) {
                return DuplicateString(customIcon, icon);
            }
        }

        const std::wstring iconPath = GetModuleDirectory() + L"\\Assets\\Logo.ico";
        return PathFileExistsW(iconPath.c_str()) ? DuplicateString(iconPath, icon) : E_NOTIMPL;
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* toolTip) override
    {
        if (toolTip) {
            *toolTip = nullptr;
        }
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* commandName) override
    {
        if (!commandName) {
            return E_POINTER;
        }
        *commandName = MakeSlotClsid(slot_);
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* state) override
    {
        if (!state) {
            return E_POINTER;
        }
        MenuEntry entry;
        *state = TryLoadMenuEntry(slot_, &entry) ? ECS_ENABLED : ECS_HIDDEN;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
    {
        MenuEntry entry;
        if (!TryLoadMenuEntry(slot_, &entry)) {
            return HRESULT_FROM_WIN32(ERROR_NOT_FOUND);
        }

        PWSTR rawPath = nullptr;
        HRESULT result = GetPathFromItems(items, &rawPath);
        if (FAILED(result)) {
            result = GetPathFromSite(site_, &rawPath);
        }
        if (FAILED(result) || !rawPath || !PathIsDirectoryW(rawPath)) {
            CoTaskMemFree(rawPath);
            return FAILED(result) ? result : HRESULT_FROM_WIN32(ERROR_DIRECTORY);
        }

        const std::wstring workingDirectory(rawPath);
        CoTaskMemFree(rawPath);

        const std::wstring launcher = GetRuntimeDirectory() + L"\\AiShellLauncher.exe";
        if (!PathFileExistsW(launcher.c_str())) {
            return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
        }

        const std::wstring launcherArguments =
            L"--run " + QuoteArgument(entry.toolId) +
            L" --mode " + QuoteArgument(entry.modeId) +
            L" --path " + QuoteArgument(workingDirectory);

        SHELLEXECUTEINFOW executeInfo = { sizeof(executeInfo) };
        executeInfo.fMask = SEE_MASK_NOASYNC;
        executeInfo.lpVerb = L"open";
        executeInfo.lpDirectory = workingDirectory.c_str();
        executeInfo.nShow = SW_SHOWNORMAL;
        executeInfo.lpFile = launcher.c_str();
        executeInfo.lpParameters = launcherArguments.c_str();

        return ShellExecuteExW(&executeInfo) ? S_OK : HRESULT_FROM_WIN32(GetLastError());
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        if (!flags) {
            return E_POINTER;
        }
        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
    {
        if (commands) {
            *commands = nullptr;
        }
        return E_NOTIMPL;
    }

    IFACEMETHODIMP SetSite(IUnknown* site) override
    {
        if (site_) {
            site_->Release();
            site_ = nullptr;
        }
        if (site) {
            site->AddRef();
            site_ = site;
        }
        return S_OK;
    }

    IFACEMETHODIMP GetSite(REFIID interfaceId, void** object) override
    {
        if (!object) {
            return E_POINTER;
        }
        *object = nullptr;
        return site_ ? site_->QueryInterface(interfaceId, object) : E_FAIL;
    }

private:
    ~SlotExplorerCommand()
    {
        if (site_) {
            site_->Release();
        }
        InterlockedDecrement(&g_moduleReferences);
    }

    long references_;
    IUnknown* site_;
    DWORD slot_;
};

class SlotClassFactory final : public IClassFactory
{
public:
    explicit SlotClassFactory(DWORD slot) : references_(1), slot_(slot)
    {
        InterlockedIncrement(&g_moduleReferences);
    }

    IFACEMETHODIMP QueryInterface(REFIID interfaceId, void** object) override
    {
        if (!object) {
            return E_POINTER;
        }
        *object = nullptr;
        if (interfaceId == IID_IUnknown || interfaceId == IID_IClassFactory) {
            *object = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }
        return E_NOINTERFACE;
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&references_);
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const ULONG references = InterlockedDecrement(&references_);
        if (references == 0) {
            delete this;
        }
        return references;
    }

    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID interfaceId, void** object) override
    {
        if (outer) {
            return CLASS_E_NOAGGREGATION;
        }

        auto command = new (std::nothrow) SlotExplorerCommand(slot_);
        if (!command) {
            return E_OUTOFMEMORY;
        }
        const HRESULT result = command->QueryInterface(interfaceId, object);
        command->Release();
        return result;
    }

    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        if (lock) {
            InterlockedIncrement(&g_moduleReferences);
        } else {
            InterlockedDecrement(&g_moduleReferences);
        }
        return S_OK;
    }

private:
    ~SlotClassFactory()
    {
        InterlockedDecrement(&g_moduleReferences);
    }

    long references_;
    DWORD slot_;
};

extern "C" BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH) {
        g_module = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}

STDAPI DllCanUnloadNow()
{
    return g_moduleReferences == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID classId, REFIID interfaceId, void** object)
{
    DWORD slot = 0;
    if (!TryGetSlotIndex(classId, &slot)) {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    auto factory = new (std::nothrow) SlotClassFactory(slot);
    if (!factory) {
        return E_OUTOFMEMORY;
    }
    const HRESULT result = factory->QueryInterface(interfaceId, object);
    factory->Release();
    return result;
}

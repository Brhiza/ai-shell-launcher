#include <windows.h>
#include <shobjidl.h>

#include <iostream>

static CLSID MakeSlotClsid(DWORD slot)
{
    CLSID value =
    { 0x4b7a4183, 0x5b26, 0x46c7, { 0xa8, 0xbf, 0x01, 0xcf, 0xe8, 0xb1, 0x33, 0x00 } };
    value.Data4[7] = static_cast<unsigned char>(slot);
    return value;
}

static void PrintResult(const wchar_t* operation, HRESULT result)
{
    std::wcout << L"  " << operation << L": 0x" << std::hex << static_cast<unsigned long>(result) << std::dec << std::endl;
}

int wmain()
{
    HRESULT result = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(result)) {
        PrintResult(L"CoInitializeEx", result);
        return 1;
    }

    int exitCode = 0;
    for (DWORD slot = 0; slot < 3; ++slot) {
        std::wcout << L"Slot " << slot << std::endl;
        IExplorerCommand* command = nullptr;
        result = CoCreateInstance(MakeSlotClsid(slot), nullptr, CLSCTX_LOCAL_SERVER, IID_PPV_ARGS(&command));
        PrintResult(L"CoCreateInstance", result);
        if (FAILED(result)) {
            exitCode = 1;
            continue;
        }

        PWSTR title = nullptr;
        result = command->GetTitle(nullptr, &title);
        PrintResult(L"GetTitle", result);
        if (SUCCEEDED(result) && title) {
            std::wcout << L"    title-length=" << wcslen(title) << std::endl;
        }
        CoTaskMemFree(title);

        PWSTR icon = nullptr;
        result = command->GetIcon(nullptr, &icon);
        PrintResult(L"GetIcon", result);
        if (SUCCEEDED(result) && icon) {
            std::wcout << L"    icon=" << icon << std::endl;
            HICON loadedIcon = static_cast<HICON>(LoadImageW(
                nullptr,
                icon,
                IMAGE_ICON,
                32,
                32,
                LR_LOADFROMFILE));
            if (loadedIcon) {
                std::wcout << L"    icon-load=ok" << std::endl;
                DestroyIcon(loadedIcon);
            } else {
                std::wcout << L"    icon-load=failed error=" << GetLastError() << std::endl;
                exitCode = 1;
            }
        } else {
            exitCode = 1;
        }
        CoTaskMemFree(icon);

        EXPCMDSTATE state = ECS_DISABLED;
        result = command->GetState(nullptr, FALSE, &state);
        PrintResult(L"GetState", result);
        std::wcout << L"    state=" << state << std::endl;

        EXPCMDFLAGS flags = ECF_DEFAULT;
        result = command->GetFlags(&flags);
        PrintResult(L"GetFlags", result);
        std::wcout << L"    flags=" << flags << std::endl;

        command->Release();
    }

    CoUninitialize();
    return exitCode;
}

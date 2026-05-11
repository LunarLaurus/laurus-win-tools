use std::ffi::OsStr;
use std::fs::File;
use std::io::{self, Read};
use std::mem;
use std::ptr;
use std::sync::mpsc;
use std::thread;
use std::time::Duration;

use windows::Win32::Foundation::*;
use windows::Win32::System::Com::*;
use windows::Win32::System::SystemInformation::*;
use windows::Win32::UI::Shell::*;
use windows::Win32::UI::WindowsAndMessaging::*;
use windows::Win32::Media::Audio::*;

const TRAY_ICON_ID: uptr = 1;
const TRAY_TIMER_ID: uptr = 1;
const TRAY_TOOLTIP_LENGTH: usize = 128;

fn main() -> io::Result<()> {
    unsafe {
        // Initialize COM
        CoInitializeEx(None, COINIT_APARTMENTTHREADED).ok();

        // Create window class
        let hinstance = GetModuleHandleW(None)?;
        let wnd_class = {
            let mut wc: WNDCLASSW = mem::zeroed();
            wc.lpfnWndProc = Some(window_proc);
            wc.hInstance = hinstance;
            wc.lpszClassName = w!("SoundTrackerWndClass");
            wc
        };
        let atom = RegisterClassW(&wnd_class);
        if atom == 0 {
            return Err(io::Error::last_os_error());
        }

        // Create hidden window
        let hwnd = CreateWindowExW(
            WINDOW_EX_STYLE::default(),
            w!(atom as PCWSTR),
            w!("Sound Tracker"),
            WS_OVERLAPPEDWINDOW,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            CW_USEDEFAULT,
            None,
            None,
            hinstance,
            None,
        );
        if hwnd.is_invalid() {
            return Err(io::Error::last_os_error());
        }

        // Create tray icon
        create_tray_icon(hwnd, hinstance)?;

        // Set timer for updating tooltip
        SetTimer(hwnd, TRAY_TIMER_ID, 1000, None); // 1 second

        // Message loop
        let mut msg = mem::MaybeUninit::zeroed();
        loop {
            let ret = GetMessageW(msg.as_mut_ptr(), HWND::default(), 0, 0);
            if ret.into() {
                TranslateMessage(msg.as_ptr());
                DispatchMessageW(msg.as_ptr());
            } else {
                break;
            }
        }

        // Cleanup
        KillTimer(hwnd, TRAY_TIMER_ID);
        DestroyWindow(hwnd);
        UnregisterClassW(w!(atom as PCWSTR), hinstance);
        CoUninitialize();
    }
    Ok(())
}

unsafe extern "system" fn window_proc(
    hwnd: HWND,
    msg: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    match msg {
        WM_TIMER => {
            if wparam.0 == TRAY_TIMER_ID {
                update_tooltip(hwnd);
            }
            LRESULT::default()
        }
        WM_DESTROY => {
            PostQuitMessage(0);
            LRESULT::default()
        }
        WM_USER => {
            // Tray icon callback
            match lparam.0 as u32 {
                WM_LBUTTONDOWN => {
                    // Left click - show tooltip (already shown on hover)
                }
                WM_RBUTTONDOWN => {
                    // Right click - show context menu (simplified: exit)
                    PostMessageW(hwnd, WM_CLOSE, WPARAM::default(), LPARAM::default());
                }
                _ => {}
            }
            LRESULT::default()
        }
        _ => DefWindowProcW(hwnd, msg, wparam, lparam),
    }
}

unsafe fn create_tray_icon(hwnd: HWND, hinstance: HINSTANCE) -> io::Result<()> {
    let mut nid: NOTIFYICONDATAW = mem::zeroed();
    nid.cbSize = mem::size_of::<NOTIFYICONDATAW>() as u32;
    nid.hWnd = hwnd;
    nid.uID = TRAY_ICON_ID;
    nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    nid.uCallbackMessage = WM_USER;
    nid.hIcon = LoadIconW(hinstance, IDI_APPLICATION)?; // Default icon for now
    // Set initial tooltip
    let tooltip = b"No recent audio\0";
    let wide: Vec<u16> = tooltip.iter().map(|&b| b as u16).chain(std::iter::once(0)).collect();
    let ptr = wide.as_ptr() as *mut u16;
    ptr::copy_nonoverlapping(ptr, nid.szTip.as_mut_ptr(), tooltip.len().min(TRAY_TOOLTIP_LENGTH - 1));
    nid.szTip[TRAY_TOOLTIP_LENGTH - 1] = 0;

    if Shell_NotifyIconW(NIM_ADD, &nid).as_bool() {
        Ok(())
    } else {
        Err(io::Error::last_os_error())
    }
}

unsafe fn update_tooltip(hwnd: HWND) {
    // Get recent audio sessions
    let recent_sessions = get_recent_audio_sessions();
    let tooltip_text = if recent_sessions.is_empty() {
        "No recent audio".to_string()
    } else {
        format!(
            "Recent audio:\n{}",
            recent_sessions
                .iter()
                .take(10)
                .map(|s| s.as_str())
                .collect::<Vec<_>>()
                .join("\n")
        )
    };

    // Convert to wide string for tooltip
    let mut wide: Vec<u16> = tooltip_text
        .encode_utf16()
        .chain(std::iter::once(0))
        .collect();
    if wide.len() > TRAY_TOOLTIP_LENGTH {
        wide.truncate(TRAY_TOOLTIP_LENGTH - 1);
        wide.push(0);
    }

    // Update NOTIFYICONDATA
    let mut nid: NOTIFYICONDATAW = mem::zeroed();
    nid.cbSize = mem::size_of::<NOTIFYICONDATAW>() as u32;
    nid.hWnd = hwnd;
    nid.uID = TRAY_ICON_ID;
    nid.uFlags = NIF_TIP;
    ptr::copy_nonoverlapping(wide.as_ptr(), nid.szTip.as_mut_ptr(), wide.len().min(TRAY_TOOLTIP_LENGTH));
    nid.szTip[TRAY_TOOLTIP_LENGTH - 1] = 0;

    Shell_NotifyIconW(NIM_MODIFY, &nid);
}

unsafe fn get_recent_audio_sessions() -> Vec<String> {
    let mut sessions = Vec::new();

    // Get default audio endpoint (render)
    let mut mm_device_enumerator: Option<IMMDeviceEnumerator> = None;
    if CoCreateInstance(
        &MMDeviceEnumerator,
        None,
        CLSCTX_INPROC_SERVER,
        &mut mm_device_enumerator,
    )
    .is_ok()
    {
        if let Some(enumerator) = mm_device_enumerator {
            let mut default_device: Option<IMMDevice> = None;
            if enumerator
                GetDefaultAudioEndpoint(eRender, eConsole, &mut default_device)
                .is_ok()
            {
                if let Some(device) = default_device {
                    // Get session manager
                    let mut session_manager: Option<IAudioSessionManager2> = None;
                    if device
                        .Activate(
                            &IAudioSessionManager2::uuids()[0],
                            CLSCTX_INPROC_SERVER,
                            None,
                            Some(&mut session_manager as *mut _ as *mut _),
                        )
                        .is_ok()
                    {
                        if let Some(manager) = session_manager {
                            // Get session enumerator
                            let mut session_enumerator: Option<IAudioSessionEnumerator> = None;
                            if manager
                                .GetSessionEnumerator(&mut session_enumerator)
                                .is_ok()
                            {
                                if let Some(enumerator) = session_enumerator {
                                    let mut count = 0;
                                    if enumerator.GetCount(&mut count).is_ok() {
                                        for i in 0..count {
                                            let mut session: Option<IAudioSessionControl> = None;
                                            if enumerator.GetSession(i, &mut session).is_ok() {
                                                if let Some(ctrl) = session {
                                                    // Get session state
                                                    let mut state = 0;
                                                    if ctrl.GetState(&mut state).is_ok()
                                                        && state == AudioSessionStateActive as u32
                                                    {
                                                        // Get process ID
                                                        let mut process_id = 0;
                                                        if ctrl.GetProcessId(&mut process_id).is_ok() && process_id != 0 {
                                                            // Get process name
                                                            if let Ok(name) = get_process_name(process_id) {
                                                                sessions.push(name);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    sessions
}

unsafe fn get_process_name(process_id: u32) -> io::Result<String> {
    let snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)?;
    if snapshot.is_invalid() {
        return Err(io::Error::last_os_error());
    }

    let mut pe32: PROCESSENTRY32W = mem::zeroed();
    pe32.dwSize = mem::size_of::<PROCESSENTRY32W>() as u32;

    if Process32FirstW(snapshot, &mut pe32).as_bool() {
        loop {
            if pe32.th32ProcessID == process_id {
                // Found the process, get executable name
                let mut name = String::from_utf16_lossy(&pe32.szExeFile);
                name = name.trim_matches(char::from(0)).to_string();
                CloseHandle(snapshot)?;
                return Ok(name);
            }
            if !Process32NextW(snapshot, &mut pe32).as_bool() {
                break;
            }
        }
    }

    CloseHandle(snapshot)?;
    Err(io::Error::new(
        io::ErrorKind::NotFound,
        format!("Process {} not found", process_id),
    ))
}
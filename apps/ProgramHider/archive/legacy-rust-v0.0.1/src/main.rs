#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]
#![allow(unsafe_op_in_unsafe_fn)]

use std::collections::HashSet;
use std::ffi::{OsStr, c_void};
use std::mem::{size_of, zeroed};
use std::os::windows::ffi::OsStrExt;
use std::ptr::{null, null_mut};

use windows_sys::Win32::Foundation::{HWND, LPARAM, LRESULT, POINT, WPARAM};
use windows_sys::Win32::System::LibraryLoader::GetModuleHandleW;
use windows_sys::Win32::UI::Input::KeyboardAndMouse::{
    MOD_CONTROL, MOD_NOREPEAT, MOD_SHIFT, RegisterHotKey, UnregisterHotKey,
};
use windows_sys::Win32::UI::Shell::{
    NIF_ICON, NIF_MESSAGE, NIF_TIP, NIM_ADD, NIM_DELETE, NIM_SETVERSION, NIN_SELECT,
    NOTIFYICON_VERSION_4, NOTIFYICONDATAW, Shell_NotifyIconW,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    AppendMenuW, CREATESTRUCTW, CS_HREDRAW, CS_VREDRAW, CreatePopupMenu, CreateWindowExW,
    DefWindowProcW, DestroyMenu, DestroyWindow, DispatchMessageW, EnumWindows, GW_OWNER,
    GWL_EXSTYLE, GWLP_USERDATA, GetClassNameW, GetCursorPos, GetForegroundWindow, GetMessageW,
    GetWindow, GetWindowLongPtrW, GetWindowLongW, GetWindowPlacement, GetWindowTextLengthW,
    GetWindowTextW, IDI_APPLICATION, IsIconic, IsWindow, IsWindowVisible, LoadIconW, MF_DISABLED,
    MF_GRAYED, MF_POPUP, MF_SEPARATOR, MF_STRING, MSG, PostMessageW, PostQuitMessage,
    RegisterClassW, SW_HIDE, SW_RESTORE, SW_SHOWMAXIMIZED, SetForegroundWindow, SetWindowLongPtrW,
    ShowWindow, TPM_LEFTALIGN, TPM_NONOTIFY, TPM_RETURNCMD, TPM_RIGHTBUTTON, TrackPopupMenu,
    TranslateMessage, WINDOWPLACEMENT, WM_APP, WM_COMMAND, WM_CONTEXTMENU, WM_CREATE, WM_DESTROY,
    WM_HOTKEY, WM_LBUTTONUP, WM_NCCREATE, WM_NCDESTROY, WM_NULL, WM_RBUTTONUP, WM_USER, WNDCLASSW,
    WS_EX_TOOLWINDOW, WS_OVERLAPPEDWINDOW,
};

const APP_NAME: &str = "Program Hider";
const WINDOW_CLASS_NAME: &str = "ProgramHiderMessageWindow";
const TRAY_ICON_ID: u32 = 1;
const HOTKEY_ID: i32 = 1;
const HOTKEY_VK_H: u32 = b'H' as u32;
const WM_TRAYICON: u32 = WM_USER + 1;
const WM_APP_INIT: u32 = WM_APP + 2;

const CMD_HIDE_ACTIVE: u32 = 100;
const CMD_RESTORE_ALL: u32 = 101;
const CMD_EXIT: u32 = 102;
const CMD_VISIBLE_BASE: u32 = 1000;
const CMD_HIDDEN_BASE: u32 = 2000;

macro_rules! debug_log {
    ($($arg:tt)*) => {
        #[cfg(debug_assertions)]
        eprintln!($($arg)*);
    };
}

#[derive(Clone, Copy)]
struct HiddenWindow {
    hwnd: HWND,
    was_maximized: bool,
}

#[derive(Clone, Copy)]
struct MenuWindowEntry {
    hwnd: HWND,
}

struct AppState {
    hidden_windows: Vec<HiddenWindow>,
}

impl AppState {
    fn new() -> Self {
        Self {
            hidden_windows: Vec::new(),
        }
    }

    fn is_hidden(&self, hwnd: HWND) -> bool {
        self.hidden_windows.iter().any(|entry| entry.hwnd == hwnd)
    }

    fn track_hidden_window(&mut self, hwnd: HWND, was_maximized: bool) {
        if self.is_hidden(hwnd) {
            return;
        }

        self.hidden_windows.push(HiddenWindow {
            hwnd,
            was_maximized,
        });
    }

    fn take_hidden_window(&mut self, hwnd: HWND) -> Option<HiddenWindow> {
        let index = self
            .hidden_windows
            .iter()
            .position(|entry| entry.hwnd == hwnd)?;
        Some(self.hidden_windows.remove(index))
    }

    fn prune_invalid_windows(&mut self) {
        self.hidden_windows
            .retain(|entry| unsafe { IsWindow(entry.hwnd) != 0 });
    }
}

fn main() {
    if let Err(message) = run() {
        eprintln!("{message}");
    }
}

fn run() -> Result<(), String> {
    unsafe {
        debug_log!("run: start");
        let instance = GetModuleHandleW(null());
        if instance.is_null() {
            return Err("GetModuleHandleW failed".to_string());
        }
        debug_log!("run: got module handle");

        let class_name = wide_null(WINDOW_CLASS_NAME);
        let tray_tip = wide_null(APP_NAME);

        let window_class = WNDCLASSW {
            style: CS_HREDRAW | CS_VREDRAW,
            lpfnWndProc: Some(window_proc),
            hInstance: instance,
            lpszClassName: class_name.as_ptr(),
            hIcon: LoadIconW(null_mut(), IDI_APPLICATION),
            ..zeroed()
        };

        if RegisterClassW(&window_class) == 0 {
            return Err("RegisterClassW failed".to_string());
        }
        debug_log!("run: registered class");

        let state = Box::new(AppState::new());
        let state_ptr = Box::into_raw(state);

        let hwnd = CreateWindowExW(
            0,
            class_name.as_ptr(),
            tray_tip.as_ptr(),
            WS_OVERLAPPEDWINDOW,
            0,
            0,
            0,
            0,
            null_mut(),
            null_mut(),
            instance,
            state_ptr.cast::<c_void>(),
        );

        if hwnd.is_null() {
            return Err("CreateWindowExW failed".to_string());
        }
        debug_log!("run: created window");

        PostMessageW(hwnd, WM_APP_INIT, 0, 0);
        debug_log!("run: posted app init");

        let mut message = zeroed::<MSG>();
        debug_log!("run: entering message loop");
        while GetMessageW(&mut message, null_mut(), 0, 0) > 0 {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
        debug_log!("run: message loop exited");
    }

    Ok(())
}

unsafe extern "system" fn window_proc(
    hwnd: HWND,
    message: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    match message {
        WM_NCCREATE => {
            let create_struct = unsafe { &*(lparam as *const CREATESTRUCTW) };
            unsafe {
                SetWindowLongPtrW(hwnd, GWLP_USERDATA, create_struct.lpCreateParams as isize);
            }
            1
        }
        WM_CREATE => {
            debug_log!("WM_CREATE: begin");
            if let Some(state) = unsafe { state_from_hwnd(hwnd) } {
                state.prune_invalid_windows();
            }
            0
        }
        WM_APP_INIT => {
            debug_log!("WM_APP_INIT");
            if unsafe { add_tray_icon(hwnd) }.is_err() {
                debug_log!(
                    "WM_APP_INIT: add_tray_icon failed: {}",
                    std::io::Error::last_os_error()
                );
                unsafe {
                    DestroyWindow(hwnd);
                }
                return 0;
            }
            debug_log!("WM_APP_INIT: tray icon added");

            unsafe {
                RegisterHotKey(
                    hwnd,
                    HOTKEY_ID,
                    MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT,
                    HOTKEY_VK_H,
                );
            }
            debug_log!("WM_APP_INIT: hotkey registered");
            0
        }
        WM_HOTKEY => {
            if wparam as i32 == HOTKEY_ID {
                if let Some(state) = unsafe { state_from_hwnd(hwnd) } {
                    unsafe {
                        hide_window(hwnd, GetForegroundWindow(), state);
                    }
                }
            }
            0
        }
        WM_TRAYICON => {
            let event = lparam as u32;
            if matches!(
                event,
                WM_CONTEXTMENU | WM_RBUTTONUP | WM_LBUTTONUP | NIN_SELECT
            ) {
                if let Some(state) = unsafe { state_from_hwnd(hwnd) } {
                    unsafe {
                        show_tray_menu(hwnd, state);
                    }
                }
            }
            0
        }
        WM_COMMAND => 0,
        WM_DESTROY => {
            debug_log!("WM_DESTROY");
            if let Some(state) = unsafe { state_from_hwnd(hwnd) } {
                unsafe {
                    restore_all_hidden(state);
                    remove_tray_icon(hwnd);
                    UnregisterHotKey(hwnd, HOTKEY_ID);
                }
            }
            unsafe {
                PostQuitMessage(0);
            }
            0
        }
        WM_NCDESTROY => {
            debug_log!("WM_NCDESTROY");
            let state_ptr = unsafe { GetWindowLongPtrW(hwnd, GWLP_USERDATA) as *mut AppState };
            if !state_ptr.is_null() {
                unsafe {
                    SetWindowLongPtrW(hwnd, GWLP_USERDATA, 0);
                    drop(Box::from_raw(state_ptr));
                }
            }
            unsafe { DefWindowProcW(hwnd, message, wparam, lparam) }
        }
        _ => unsafe { DefWindowProcW(hwnd, message, wparam, lparam) },
    }
}

unsafe fn state_from_hwnd(hwnd: HWND) -> Option<&'static mut AppState> {
    let ptr = unsafe { GetWindowLongPtrW(hwnd, GWLP_USERDATA) as *mut AppState };
    if ptr.is_null() {
        None
    } else {
        Some(unsafe { &mut *ptr })
    }
}

unsafe fn add_tray_icon(hwnd: HWND) -> Result<(), ()> {
    let mut tray_icon = tray_icon_data(hwnd);
    if unsafe { Shell_NotifyIconW(NIM_ADD, &mut tray_icon) } == 0 {
        return Err(());
    }

    tray_icon.Anonymous.uVersion = NOTIFYICON_VERSION_4;
    unsafe {
        Shell_NotifyIconW(NIM_SETVERSION, &mut tray_icon);
    }

    Ok(())
}

unsafe fn remove_tray_icon(hwnd: HWND) {
    let mut tray_icon = tray_icon_data(hwnd);
    unsafe {
        Shell_NotifyIconW(NIM_DELETE, &mut tray_icon);
    }
}

unsafe fn tray_icon_data(hwnd: HWND) -> NOTIFYICONDATAW {
    let mut tray_icon = zeroed::<NOTIFYICONDATAW>();
    tray_icon.cbSize = size_of::<NOTIFYICONDATAW>() as u32;
    tray_icon.hWnd = hwnd;
    tray_icon.uID = TRAY_ICON_ID;
    tray_icon.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    tray_icon.uCallbackMessage = WM_TRAYICON;
    tray_icon.hIcon = unsafe { LoadIconW(null_mut(), IDI_APPLICATION) };
    copy_wide_into_fixed(&wide_null(APP_NAME), &mut tray_icon.szTip);
    tray_icon
}

unsafe fn show_tray_menu(hwnd: HWND, state: &mut AppState) {
    state.prune_invalid_windows();

    let visible_windows = enumerate_visible_windows(hwnd, state);
    let hidden_windows = state.hidden_windows.clone();
    let mut menu_labels: Vec<Vec<u16>> = Vec::new();

    let menu = unsafe { CreatePopupMenu() };
    let hide_menu = unsafe { CreatePopupMenu() };

    let hide_active_text = wide_null("Hide active window\tCtrl+Shift+H");
    let hide_window_text = wide_null("Hide window");
    let no_windows_text = wide_null("No eligible windows");
    let restore_all_text = wide_null("Restore all");
    let exit_text = wide_null("Exit");

    unsafe {
        AppendMenuW(
            menu,
            MF_STRING,
            CMD_HIDE_ACTIVE as usize,
            hide_active_text.as_ptr(),
        );
        AppendMenuW(
            menu,
            MF_POPUP,
            hide_menu as usize,
            hide_window_text.as_ptr(),
        );
    }

    if visible_windows.is_empty() {
        unsafe {
            AppendMenuW(
                hide_menu,
                MF_STRING | MF_DISABLED | MF_GRAYED,
                0,
                no_windows_text.as_ptr(),
            );
        }
    } else {
        for (index, window) in visible_windows.iter().enumerate() {
            let title = window_title(window.hwnd);
            menu_labels.push(wide_null(&truncate_for_menu(&title)));
            let menu_label = menu_labels.last().unwrap();
            unsafe {
                AppendMenuW(
                    hide_menu,
                    MF_STRING,
                    (CMD_VISIBLE_BASE + index as u32) as usize,
                    menu_label.as_ptr(),
                );
            }
        }
    }

    unsafe {
        AppendMenuW(menu, MF_SEPARATOR, 0, null());
    }

    if hidden_windows.is_empty() {
        let nothing_hidden_text = wide_null("No hidden windows");
        unsafe {
            AppendMenuW(
                menu,
                MF_STRING | MF_DISABLED | MF_GRAYED,
                0,
                nothing_hidden_text.as_ptr(),
            );
        }
    } else {
        for (index, window) in hidden_windows.iter().enumerate() {
            let title = window_title(window.hwnd);
            let label = format!("Restore: {}", truncate_for_menu(&title));
            menu_labels.push(wide_null(&label));
            let menu_label = menu_labels.last().unwrap();
            unsafe {
                AppendMenuW(
                    menu,
                    MF_STRING,
                    (CMD_HIDDEN_BASE + index as u32) as usize,
                    menu_label.as_ptr(),
                );
            }
        }
    }

    unsafe {
        AppendMenuW(menu, MF_SEPARATOR, 0, null());
        AppendMenuW(
            menu,
            MF_STRING,
            CMD_RESTORE_ALL as usize,
            restore_all_text.as_ptr(),
        );
        AppendMenuW(menu, MF_STRING, CMD_EXIT as usize, exit_text.as_ptr());
    }

    let mut cursor = POINT { x: 0, y: 0 };
    unsafe {
        GetCursorPos(&mut cursor);
        SetForegroundWindow(hwnd);
    }

    let command = unsafe {
        TrackPopupMenu(
            menu,
            TPM_LEFTALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_NONOTIFY,
            cursor.x,
            cursor.y,
            0,
            hwnd,
            null(),
        )
    };

    if command != 0 {
        unsafe {
            handle_menu_command(
                hwnd,
                command as u32,
                state,
                &visible_windows,
                &hidden_windows,
            );
        }
    }

    unsafe {
        DestroyMenu(menu);
        PostMessageW(hwnd, WM_NULL, 0, 0);
    }
}

unsafe fn handle_menu_command(
    hwnd: HWND,
    command: u32,
    state: &mut AppState,
    visible_windows: &[MenuWindowEntry],
    hidden_windows: &[HiddenWindow],
) {
    match command {
        CMD_HIDE_ACTIVE => unsafe {
            hide_window(hwnd, GetForegroundWindow(), state);
        },
        CMD_RESTORE_ALL => unsafe {
            restore_all_hidden(state);
        },
        CMD_EXIT => unsafe {
            DestroyWindow(hwnd);
        },
        visible
            if visible >= CMD_VISIBLE_BASE
                && visible < CMD_VISIBLE_BASE + visible_windows.len() as u32 =>
        {
            let index = (visible - CMD_VISIBLE_BASE) as usize;
            unsafe {
                hide_window(hwnd, visible_windows[index].hwnd, state);
            }
        }
        hidden
            if hidden >= CMD_HIDDEN_BASE
                && hidden < CMD_HIDDEN_BASE + hidden_windows.len() as u32 =>
        {
            let index = (hidden - CMD_HIDDEN_BASE) as usize;
            unsafe {
                restore_window(hidden_windows[index].hwnd, state);
            }
        }
        _ => {}
    }
}

unsafe fn hide_window(owner_hwnd: HWND, target_hwnd: HWND, state: &mut AppState) {
    if !is_hideable_window(owner_hwnd, target_hwnd, state) {
        return;
    }

    let was_maximized = unsafe { IsIconic(target_hwnd) == 0 && is_window_maximized(target_hwnd) };
    unsafe {
        ShowWindow(target_hwnd, SW_HIDE);
    }
    state.track_hidden_window(target_hwnd, was_maximized);
}

unsafe fn restore_window(target_hwnd: HWND, state: &mut AppState) {
    let Some(hidden_window) = state.take_hidden_window(target_hwnd) else {
        return;
    };

    if unsafe { IsWindow(target_hwnd) } == 0 {
        return;
    }

    unsafe {
        if hidden_window.was_maximized {
            ShowWindow(target_hwnd, SW_SHOWMAXIMIZED);
        } else {
            ShowWindow(target_hwnd, SW_RESTORE);
        }
        SetForegroundWindow(target_hwnd);
    }
}

unsafe fn restore_all_hidden(state: &mut AppState) {
    let windows: Vec<HWND> = state
        .hidden_windows
        .iter()
        .map(|entry| entry.hwnd)
        .collect();
    for hwnd in windows {
        unsafe {
            restore_window(hwnd, state);
        }
    }
}

unsafe fn enumerate_visible_windows(owner_hwnd: HWND, state: &AppState) -> Vec<MenuWindowEntry> {
    let mut windows = Vec::<MenuWindowEntry>::new();
    let mut context = EnumContext {
        owner_hwnd,
        hidden_hwnds: state
            .hidden_windows
            .iter()
            .map(|entry| entry.hwnd)
            .collect(),
        windows: &mut windows,
    };

    unsafe {
        EnumWindows(
            Some(enum_windows_proc),
            (&mut context as *mut EnumContext).cast::<c_void>() as isize,
        );
    }

    windows
}

struct EnumContext<'a> {
    owner_hwnd: HWND,
    hidden_hwnds: HashSet<HWND>,
    windows: &'a mut Vec<MenuWindowEntry>,
}

unsafe extern "system" fn enum_windows_proc(hwnd: HWND, lparam: LPARAM) -> i32 {
    let context = unsafe { &mut *(lparam as *mut EnumContext<'_>) };
    if !unsafe { is_candidate_window(context.owner_hwnd, hwnd, &context.hidden_hwnds) } {
        return 1;
    }

    context.windows.push(MenuWindowEntry { hwnd });
    1
}

unsafe fn is_hideable_window(owner_hwnd: HWND, hwnd: HWND, state: &AppState) -> bool {
    if hwnd.is_null() || hwnd == owner_hwnd || state.is_hidden(hwnd) {
        return false;
    }

    let hidden_hwnds: HashSet<HWND> = state
        .hidden_windows
        .iter()
        .map(|entry| entry.hwnd)
        .collect();
    unsafe { is_candidate_window(owner_hwnd, hwnd, &hidden_hwnds) }
}

unsafe fn is_candidate_window(owner_hwnd: HWND, hwnd: HWND, hidden_hwnds: &HashSet<HWND>) -> bool {
    if hwnd.is_null() || hwnd == owner_hwnd || hidden_hwnds.contains(&hwnd) {
        return false;
    }

    if unsafe { IsWindow(hwnd) } == 0 || unsafe { IsWindowVisible(hwnd) } == 0 {
        return false;
    }

    if unsafe { GetWindow(hwnd, GW_OWNER) }.is_null() {
        let ex_style = unsafe { GetWindowLongW(hwnd, GWL_EXSTYLE) as u32 };
        if ex_style & WS_EX_TOOLWINDOW != 0 {
            return false;
        }
    } else {
        return false;
    }

    let title = window_title(hwnd);
    if title.trim().is_empty() {
        return false;
    }

    let class_name = window_class_name(hwnd);
    !matches!(class_name.as_str(), "Shell_TrayWnd" | "Progman")
}

unsafe fn is_window_maximized(hwnd: HWND) -> bool {
    let mut placement = zeroed::<WINDOWPLACEMENT>();
    placement.length = size_of::<WINDOWPLACEMENT>() as u32;
    if unsafe { GetWindowPlacement(hwnd, &mut placement) } == 0 {
        return false;
    }

    placement.showCmd == SW_SHOWMAXIMIZED as u32
}

fn window_title(hwnd: HWND) -> String {
    unsafe {
        let length = GetWindowTextLengthW(hwnd);
        if length <= 0 {
            return String::new();
        }

        let mut buffer = vec![0u16; length as usize + 1];
        let copied = GetWindowTextW(hwnd, buffer.as_mut_ptr(), buffer.len() as i32);
        String::from_utf16_lossy(&buffer[..copied as usize])
    }
}

fn window_class_name(hwnd: HWND) -> String {
    unsafe {
        let mut buffer = vec![0u16; 256];
        let copied = GetClassNameW(hwnd, buffer.as_mut_ptr(), buffer.len() as i32);
        String::from_utf16_lossy(&buffer[..copied as usize])
    }
}

fn truncate_for_menu(title: &str) -> String {
    const MAX_LEN: usize = 60;
    let escaped = title.replace('&', "&&");
    let mut chars = escaped.chars();
    let collected: String = chars.by_ref().take(MAX_LEN).collect();
    if chars.next().is_some() {
        format!("{collected}...")
    } else {
        collected
    }
}

fn wide_null(value: &str) -> Vec<u16> {
    OsStr::new(value).encode_wide().chain(Some(0)).collect()
}

fn copy_wide_into_fixed<const N: usize>(source: &[u16], destination: &mut [u16; N]) {
    let length = source.len().min(destination.len());
    destination[..length].copy_from_slice(&source[..length]);
    if length == destination.len() {
        destination[N - 1] = 0;
    }
}

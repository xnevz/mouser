# Windows Helper Service: UAC Secure Desktop and Lock Screen Input

## 1. Problem Statement

On Windows, standard user processes operate under User Interface Privilege Isolation (UIPI):
1. **Elevated Windows:** A standard user process cannot send synthetic input (`SendInput`, mouse hooks) to windows running with higher integrity levels (such as Task Manager, Administrator command prompts, and installers).
2. **UAC Secure Desktop:** When a User Account Control prompt appears, Windows switches the active display from the default desktop (`winsta0\default`) to the secure desktop (`winsta0\winlogon`). Normal user-mode processes cannot interact with or receive hooks from the secure desktop.
3. **Lock Screen:** When the workstation is locked (`Win+L`), the display switches to `winsta0\winlogon`. A standard background process cannot type the unlock password or move the pointer.

To allow remote mouse and keyboard control to seamlessly navigate UAC prompts and unlock locked PCs, Mouser requires an elevated helper component running with `LocalSystem` privileges.

---

## 2. System Architecture

```
+-----------------------------------------------------------+
| Windows Service (LocalSystem)                             |
| Mouser.Service.exe                                        |
|   - Monitors active console sessions                      |
|   - Launches helper into winlogon desktop                 |
+-----------------------------+-----------------------------+
                              | CreateProcessAsUser("winsta0\winlogon")
                              v
+-----------------------------------------------------------+
| Session Helper Process (System / Active Console Session)   |
| Mouser.ElevatedHelper.exe                                 |
|   - Runs on winlogon desktop                              |
|   - Exposes local Named Pipe with strict interactive ACL  |
|   - Injects input via SendInput()                         |
+-----------------------------^-----------------------------+
                              |
                              | Named Pipe (\\.\pipe\Mouser_Helper_{SessionId})
                              |
+-----------------------------+-----------------------------+
| Interactive User Process                                   |
| Mouser.exe (Standard Integrity)                           |
|   - UDP discovery & Protocol v2 KVM engine                |
|   - Connects to pipe when target window is elevated       |
+-----------------------------------------------------------+
```

### Key Components

1. **Windows Service (`Mouser.Service.exe`):**
   - Configured with `start= auto`, running as `NT AUTHORITY\LocalSystem`.
   - Listens to terminal services events via `WTSRegisterSessionNotification`.
   - On console session activation (`WTS_CONSOLE_CONNECT`), queries active session ID (`WTSGetActiveConsoleSessionId`).
   - Duplicates the session token (`WTSQueryUserToken` or system token with session ID set via `SetTokenInformation(TokenSessionId)`).
   - Spawns `Mouser.ElevatedHelper.exe` using `CreateProcessAsUser` with `STARTUPINFO.lpDesktop = @"winsta0\winlogon"`.

2. **Session Helper (`Mouser.ElevatedHelper.exe`):**
   - Resides on the `winlogon` desktop.
   - Listens on a local named pipe: `\\.\pipe\Mouser_ElevatedHelper_{SessionId}`.
   - Reads validated binary motion packets (`InputMotionPacket`, 16 bytes).
   - Calls Win32 `SendInput` to inject mouse clicks, pointer movements, and keystrokes into elevated windows and the secure desktop.

3. **User Client Bridge:**
   - In `Mouser.Core/Input/Win32InputSink.cs`:
   - If `SendInput` fails due to UIPI or desktop switch, forwards the packet over the local named pipe to the helper.

---

## 3. Security Design & Named Pipe ACL

Because the helper process runs with elevated system privileges, it must be hardened against local privilege escalation (LPE):

1. **Strict Named Pipe ACL:**
   - Security descriptor permits access **only** to:
     - `NT AUTHORITY\LocalSystem` (Full Control)
     - `Interactive User Token` corresponding to `WTSGetActiveConsoleSessionId()` (Read/Write)
   - Disallow Network (`Deny Network`), disallow Everyone, disallow anonymous callers.
2. **Minimal Code Surface:**
   - The helper contains **no networking code**, **no file transfer logic**, and **no crypto keys**.
   - Input protocol over the pipe is strictly fixed-size structs (`InputMotionPacket` - 16 bytes). Any packet with unexpected framing, unknown opcode, or out-of-bounds coordinates is immediately dropped and the pipe connection severed.
3. **No Clipboard Sharing on Secure Desktop:**
   - Clipboard synchronization is strictly disabled while the secure desktop or lock screen is active to prevent password exfiltration.

---

## 4. Failure Modes & Graceful Fallbacks

| Scenario | Behavior | Mitigation |
| :--- | :--- | :--- |
| **Service Not Installed** | User runs portable build or uninstalled service. | Mouser continues in standard user mode; UI surfaces non-blocking banner ("Admin windows/UAC require elevated service"). |
| **Service Stopped / Crashed** | Pipe connection fails (`FileNotFoundException` / broken pipe). | Mouser falls back to normal user-mode `SendInput`. Logged as diagnostic warning. |
| **Fast User Switching** | Active console session changes from User A to User B. | Service detects `WTS_CONSOLE_CONNECT` / `WTS_CONSOLE_DISCONNECT`, terminates helper for User A, and spawns new helper for User B. |
| **RDP Disconnection** | Session detached. | Service halts helper input injection until local or remote console is re-attached. |

---

## 5. Installer Integration (Inno Setup)

In `installer/windows/mouser.iss`:

```pascal
[Run]
Filename: "{sys}\sc.exe"; Parameters: "create MouserService binPath= ""{app}\Mouser.Service.exe"" start= auto displayname= ""Mouser Helper Service"""; Flags: runhidden; Check: IsAdminInstallMode
Filename: "{sys}\sc.exe"; Parameters: "start MouserService"; Flags: runhidden; Check: IsAdminInstallMode

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop MouserService"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete MouserService"; Flags: runhidden
```

---

## 6. Verification & Test Plan

1. **VM Test Environment:** Windows 11 Enterprise in Hyper-V/VMware with UAC slider set to highest level ("Always notify").
2. **Elevated Windows Test:**
   - Open elevated Task Manager and elevated Command Prompt.
   - Connect from peer computer or phone; verify pointer moves and keys register inside elevated cmd.
3. **UAC Secure Desktop Test:**
   - Trigger UAC prompt by launching `regedit.exe`.
   - Verify screen dims to secure desktop.
   - Verify remote cursor can click "Yes" / "No" or press Alt+Y.
4. **Lock Screen Test:**
   - Press `Win+L` to lock Windows.
   - Send wake key / mouse wiggle from remote device; verify lock screen wakes.
   - Enter password/PIN from remote device; verify successful login.

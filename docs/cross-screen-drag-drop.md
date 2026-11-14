# Cross-Screen Drag and Drop Design Specification (Spike E17)

## 1. Overview and Problem

Traditional software KVMs drop any in-progress mouse drag when the pointer leaves the physical monitor boundary because Windows treats standard drag-and-drop operations (`IDropTarget`, OLE Drag & Drop) as tied to a local COM window handle and mouse capture loop.

Task E17 specifies a lightweight, reliable cross-screen drag-and-drop mechanism that requires **no invasive OLE hooking or shell extensions**, utilizing existing Mouser file streaming capabilities (`FileStreamingService`) and synthetic clipboard actions.

---

## 2. Interaction Flow

```
[Local Host: Source PC]                                      [Remote Peer: Destination PC]
-----------------------                                      -----------------------------
1. User holds Left Mouse Button
   over file(s) in Windows Explorer
2. Pointer moves past monitor edge
   into Peer boundary (E9c/E10)
3. Controller detects:
   - Left button down
   - Foreground is Explorer
4. Pre-crossing Action:
   - Save current clipboard snapshot
   - Inject synthetic Ctrl+C
   - Read file URIs via clipboard
   - Check size limit (<= 500 MB)
5. Enter Peer:
   - Cursor jumps to peer (Left button remains held)
   - Background streaming begins
     via SendFileAsync()
6. User drags across peer desktop
   and releases Left Mouse Button
                                                              7. Pointer Release:
                                                                 - File streaming completes to Downloads\Mouser
                                                                 - If destination foreground is Explorer:
                                                                   Set clipboard to local file drop and inject Ctrl+V
                                                                 - If destination is desktop/other:
                                                                   Files remain in Downloads\Mouser; display Toast
```

---

## 3. Spike Research Questions & Answers

### Q1: Is the synthetic Ctrl+C trick acceptable?
- **Answer:** **Yes**, provided clipboard preservation is enforced.
- **Implementation:**
  - Before injecting `Ctrl+C`, the local Mouser engine captures a full in-memory snapshot of the existing clipboard (`ClipboardPayload`).
  - Once the file list is captured for drag streaming, Mouser restores the original clipboard payload if the drag is cancelled or after a 1-second timeout.
  - Alternatively, because Mouser includes the Smart Clipboard History (Track D), the temporary copy event can be marked with a transient flag (`is_transient = true`) so it does not pollute the user's permanent clipboard database.

### Q2: Which folder does Explorer paste into when the cursor is released?
- **Answer:**
  - **Over an Explorer item/subfolder:** When `Ctrl+V` is executed while an Explorer folder item has selection/focus, Windows Shell automatically places the pasted files inside that target folder.
  - **Over blank folder space:** Explorer pastes directly into the currently active directory path.
  - **Over desktop or non-Explorer applications:** If the destination window is not Explorer (e.g. Chrome, Slack, IDE), Mouser skips `Ctrl+V` and saves the files into the default receiving folder (`%USERPROFILE%\Downloads\Mouser`), firing a system toast:
    *"Received {filename} from {peer} (saved to Downloads\Mouser)"*.

### Q3: What is the recommended size limit for cross-screen drag?
- **Answer:** **500 MB default budget**.
- **Rationale:**
  - Drag-and-drop is an interactive real-time visual gesture. Transferring multiple gigabytes while holding the mouse button causes visual dissonance if the user releases the button before network transfer completes.
  - For larger payloads (> 500 MB), the user should use the standard file transfer dialog (Track D11 / Quick Send) or copy-paste via Smart Clipboard.
  - If a drag selection exceeds 500 MB, Mouser cancels the drag transition, returns the cursor to local bounds, and displays a toast notification:
    *"File selection ({size} MB) exceeds 500 MB drag limit. Use copy-paste instead."*

---

## 4. Policy Class & Testability

All decision logic is encapsulated in the pure static class `Mouser.Core.Kvm.DragDropPolicy`:
- `ShouldStartDrag(isLeftButtonDown, isSourceExplorer, totalSizeBytes, maxSizeBytes)`
- `IsExplorerWindow(processName, windowClassName)`
- `ExceedsSizeBudget(totalSizeBytes, maxSizeBytes)`

This ensures deterministic testing without requiring actual OS display servers, file system mocks, or COM drag loops.

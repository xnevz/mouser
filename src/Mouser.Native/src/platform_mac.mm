#include "mouser_native.h"

#ifdef __APPLE__
#import <Foundation/Foundation.h>
#import <ApplicationServices/ApplicationServices.h>

static CFMachPortRef g_eventTap = NULL;
static CFRunLoopSourceRef g_runLoopSource = NULL;
static NativeMouseMoveCallback g_mouseMoveCallback = NULL;
static NativeMouseButtonCallback g_mouseButtonCallback = NULL;
static NativeKeyCallback g_keyCallback = NULL;

static CGEventRef EventTapCallback(CGEventTapProxy proxy, CGEventType type, CGEventRef event, void *refcon) {
    if (type == kCGEventMouseMoved && g_mouseMoveCallback) {
        int64_t dx = CGEventGetIntegerValueField(event, kCGMouseEventDeltaX);
        int64_t dy = CGEventGetIntegerValueField(event, kCGMouseEventDeltaY);
        g_mouseMoveCallback((int16_t)dx, (int16_t)dy);
    } else if ((type == kCGEventLeftMouseDown || type == kCGEventLeftMouseUp) && g_mouseButtonCallback) {
        bool isDown = (type == kCGEventLeftMouseDown);
        g_mouseButtonCallback(1, isDown);
    } else if ((type == kCGEventRightMouseDown || type == kCGEventRightMouseUp) && g_mouseButtonCallback) {
        bool isDown = (type == kCGEventRightMouseDown);
        g_mouseButtonCallback(2, isDown);
    } else if ((type == kCGEventKeyDown || type == kCGEventKeyUp) && g_keyCallback) {
        int64_t keycode = CGEventGetIntegerValueField(event, kCGKeyboardEventKeycode);
        CGEventFlags flags = CGEventGetFlags(event);
        uint8_t modifiers = 0;
        if (flags & kCGEventFlagMaskCommand) modifiers |= 0x01;
        if (flags & kCGEventFlagMaskShift) modifiers |= 0x02;
        if (flags & kCGEventFlagMaskControl) modifiers |= 0x04;
        if (flags & kCGEventFlagMaskAlternate) modifiers |= 0x08;
        bool isDown = (type == kCGEventKeyDown);
        g_keyCallback((uint32_t)keycode, modifiers, isDown);
    }

    return event;
}

extern "C" {

uint32_t mouser_get_native_version(void) {
    return 0x00010000; // 1.0.0
}

int32_t mouser_native_ping(int32_t value) {
    return value * 42;
}

bool mouser_check_accessibility_permission(void) {
    return (bool)AXIsProcessTrusted();
}

int32_t mouser_init_input_capture(NativeMouseMoveCallback onMove, 
                                 NativeMouseButtonCallback onButton, 
                                 NativeKeyCallback onKey) {
    g_mouseMoveCallback = onMove;
    g_mouseButtonCallback = onButton;
    g_keyCallback = onKey;

    if (!AXIsProcessTrusted()) {
        return -1; // Not trusted for accessibility
    }

    if (g_eventTap != NULL) {
        return 0; // Already running
    }

    CGEventMask mask = CGEventMaskBit(kCGEventMouseMoved) |
                       CGEventMaskBit(kCGEventLeftMouseDown) |
                       CGEventMaskBit(kCGEventLeftMouseUp) |
                       CGEventMaskBit(kCGEventRightMouseDown) |
                       CGEventMaskBit(kCGEventRightMouseUp) |
                       CGEventMaskBit(kCGEventKeyDown) |
                       CGEventMaskBit(kCGEventKeyUp);

    g_eventTap = CGEventTapCreate(
        kCGHIDEventTap,
        kCGHeadInsertEventTap,
        kCGEventTapOptionListenOnly,
        mask,
        EventTapCallback,
        NULL
    );

    if (!g_eventTap) {
        return -2; // Tap creation failed
    }

    g_runLoopSource = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, g_eventTap, 0);
    CFRunLoopAddSource(CFRunLoopGetMain(), g_runLoopSource, kCFRunLoopCommonModes);
    CGEventTapEnable(g_eventTap, true);

    return 0;
}

void mouser_stop_input_capture(void) {
    if (g_eventTap) {
        CGEventTapEnable(g_eventTap, false);
        if (g_runLoopSource) {
            CFRunLoopRemoveSource(CFRunLoopGetMain(), g_runLoopSource, kCFRunLoopCommonModes);
            CFRelease(g_runLoopSource);
            g_runLoopSource = NULL;
        }
        CFRelease(g_eventTap);
        g_eventTap = NULL;
    }
    g_mouseMoveCallback = NULL;
    g_mouseButtonCallback = NULL;
    g_keyCallback = NULL;
}

void mouser_inject_mouse_move(int32_t targetX, int32_t targetY) {
    CGPoint target = CGPointMake((CGFloat)targetX, (CGFloat)targetY);
    CGEventRef ev = CGEventCreateMouseEvent(NULL, kCGEventMouseMoved, target, kCGMouseButtonLeft);
    if (ev) {
        CGEventPost(kCGHIDEventTap, ev);
        CFRelease(ev);
    }
}

void mouser_inject_mouse_delta(int16_t dx, int16_t dy) {
    CGEventRef currentEv = CGEventCreate(NULL);
    if (!currentEv) return;
    CGPoint currentLoc = CGEventGetLocation(currentEv);
    CFRelease(currentEv);

    CGPoint newLoc = CGPointMake(currentLoc.x + dx, currentLoc.y + dy);
    CGEventRef moveEv = CGEventCreateMouseEvent(NULL, kCGEventMouseMoved, newLoc, kCGMouseButtonLeft);
    if (moveEv) {
        CGEventSetIntegerValueField(moveEv, kCGMouseEventDeltaX, dx);
        CGEventSetIntegerValueField(moveEv, kCGMouseEventDeltaY, dy);
        CGEventPost(kCGHIDEventTap, moveEv);
        CFRelease(moveEv);
    }
}

void mouser_inject_mouse_button(uint8_t button, bool isDown) {
    CGEventRef currentEv = CGEventCreate(NULL);
    if (!currentEv) return;
    CGPoint currentLoc = CGEventGetLocation(currentEv);
    CFRelease(currentEv);

    CGEventType evType;
    CGMouseButton mouseButton = kCGMouseButtonLeft;

    if (button == 1) { // Left
        evType = isDown ? kCGEventLeftMouseDown : kCGEventLeftMouseUp;
        mouseButton = kCGMouseButtonLeft;
    } else if (button == 2) { // Right
        evType = isDown ? kCGEventRightMouseDown : kCGEventRightMouseUp;
        mouseButton = kCGMouseButtonRight;
    } else { // Middle
        evType = isDown ? kCGEventOtherMouseDown : kCGEventOtherMouseUp;
        mouseButton = kCGMouseButtonCenter;
    }

    CGEventRef btnEv = CGEventCreateMouseEvent(NULL, evType, currentLoc, mouseButton);
    if (btnEv) {
        CGEventPost(kCGHIDEventTap, btnEv);
        CFRelease(btnEv);
    }
}

void mouser_inject_key(uint32_t keyCode, uint8_t modifiers, bool isDown) {
    CGEventRef keyEv = CGEventCreateKeyboardEvent(NULL, (CGKeyCode)keyCode, (Boolean)isDown);
    if (keyEv) {
        CGEventFlags flags = 0;
        if (modifiers & 0x01) flags |= kCGEventFlagMaskCommand;
        if (modifiers & 0x02) flags |= kCGEventFlagMaskShift;
        if (modifiers & 0x04) flags |= kCGEventFlagMaskControl;
        if (modifiers & 0x08) flags |= kCGEventFlagMaskAlternate;
        CGEventSetFlags(keyEv, flags);
        CGEventPost(kCGHIDEventTap, keyEv);
        CFRelease(keyEv);
    }
}

} // extern "C"

#endif // __APPLE__

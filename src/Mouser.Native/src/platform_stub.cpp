#ifndef __APPLE__
#include "mouser_native.h"
#include <stdio.h>

extern "C" {

uint32_t mouser_get_native_version(void) {
    return 0x00010000;
}

int32_t mouser_native_ping(int32_t value) {
    return value * 42;
}

bool mouser_check_accessibility_permission(void) {
    return true;
}

int32_t mouser_init_input_capture(NativeMouseMoveCallback onMove, 
                                 NativeMouseButtonCallback onButton, 
                                 NativeKeyCallback onKey) {
    (void)onMove; (void)onButton; (void)onKey;
    return 0;
}

void mouser_stop_input_capture(void) {
}

void mouser_inject_mouse_move(int32_t targetX, int32_t targetY) {
    (void)targetX; (void)targetY;
}

void mouser_inject_mouse_delta(int16_t dx, int16_t dy) {
    (void)dx; (void)dy;
}

void mouser_inject_mouse_button(uint8_t button, bool isDown) {
    (void)button; (void)isDown;
}

void mouser_inject_key(uint32_t keyCode, uint8_t modifiers, bool isDown) {
    (void)keyCode; (void)modifiers; (void)isDown;
}

void mouser_inject_scroll(int32_t horizontal, int32_t vertical) {
    (void)horizontal; (void)vertical;
}

static bool g_stubInputRedirected = false;
static int32_t g_stubCursorX = 0;
static int32_t g_stubCursorY = 0;

void mouser_set_input_redirection(bool enabled) {
    g_stubInputRedirected = enabled;
}

bool mouser_is_input_redirected(void) {
    return g_stubInputRedirected;
}

void mouser_get_cursor_position(int32_t *outX, int32_t *outY) {
    if (outX) *outX = g_stubCursorX;
    if (outY) *outY = g_stubCursorY;
}

void mouser_set_cursor_position(int32_t x, int32_t y) {
    g_stubCursorX = x;
    g_stubCursorY = y;
}

} // extern "C"

#endif // !__APPLE__

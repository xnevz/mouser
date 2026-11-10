#ifndef MOUSER_NATIVE_H
#define MOUSER_NATIVE_H

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

// Function pointer signatures for C# callbacks
typedef void (*NativeMouseMoveCallback)(int16_t dx, int16_t dy);
typedef void (*NativeMouseButtonCallback)(uint8_t button, bool isDown);
typedef void (*NativeKeyCallback)(uint32_t keyCode, uint8_t modifiers, bool isDown);

// Platform Lifecycle & Permission APIs
bool mouser_check_accessibility_permission(void);
int32_t mouser_init_input_capture(NativeMouseMoveCallback onMove, 
                                 NativeMouseButtonCallback onButton, 
                                 NativeKeyCallback onKey);
void mouser_stop_input_capture(void);

// Synthetic Event Injection APIs
void mouser_inject_mouse_move(int32_t targetX, int32_t targetY);
void mouser_inject_mouse_delta(int16_t dx, int16_t dy);
void mouser_inject_mouse_button(uint8_t button, bool isDown);
void mouser_inject_key(uint32_t keyCode, uint8_t modifiers, bool isDown);

// Diagnostic & Version APIs
uint32_t mouser_get_native_version(void);
int32_t mouser_native_ping(int32_t value);

#ifdef __cplusplus
}
#endif

#endif // MOUSER_NATIVE_H

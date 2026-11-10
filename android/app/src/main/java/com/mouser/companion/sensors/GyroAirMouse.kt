package com.mouser.companion.sensors

import android.content.Context
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import kotlin.math.abs

/**
 * 3D Gyroscope and IMU sensor listener that calculates spatial air-mouse deltas for desktop cursor control.
 * Includes micro-tremor deadzone filtering and dynamic sensitivity scaling.
 */
class GyroAirMouse(
    context: Context,
    private val onDeltaCalculated: (dx: Short, dy: Short) -> Unit
) : SensorEventListener {

    private val sensorManager = context.getSystemService(Context.SENSOR_SERVICE) as SensorManager
    private val gyroSensor: Sensor? = sensorManager.getDefaultSensor(Sensor.TYPE_GYROSCOPE)

    var isEnabled: Boolean = false
        private set

    var isTriggerHeld: Boolean = false
    var sensitivity: Float = 25.0f
    var deadzoneThreshold: Float = 0.04f

    fun start() {
        if (isEnabled || gyroSensor == null) return
        isEnabled = true
        sensorManager.registerListener(
            this,
            gyroSensor,
            SensorManager.SENSOR_DELAY_GAME
        )
    }

    fun stop() {
        if (!isEnabled) return
        isEnabled = false
        sensorManager.unregisterListener(this)
    }

    override fun onSensorChanged(event: SensorEvent?) {
        if (event == null || !isEnabled || !isTriggerHeld) return

        if (event.sensor.type == Sensor.TYPE_GYROSCOPE) {
            // Values: [0] = Angular speed around X, [1] = Y, [2] = Z in rad/s
            val yawVelocity = event.values[2]   // Z-axis rotation (left/right tilt)
            val pitchVelocity = event.values[0] // X-axis rotation (up/down tilt)

            // Deadzone filter: suppress hand micro-tremors below threshold
            if (abs(yawVelocity) > deadzoneThreshold || abs(pitchVelocity) > deadzoneThreshold) {
                // Invert axes according to natural pointer ergonomics
                val dx = (-yawVelocity * sensitivity).toInt().coerceIn(-32768, 32767).toShort()
                val dy = (-pitchVelocity * sensitivity).toInt().coerceIn(-32768, 32767).toShort()

                if (dx != 0.toShort() || dy != 0.toShort()) {
                    onDeltaCalculated(dx, dy)
                }
            }
        }
    }

    override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) {}
}

package com.mouser.companion.sensors

import android.content.Context
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import kotlin.math.abs

/**
 * Turns gyroscope rotation into pointer motion. The sensor is only registered while the trigger is held,
 * so the air mouse costs no battery the rest of the time.
 */
class GyroAirMouse(
    context: Context,
    private val onDelta: (dx: Float, dy: Float) -> Unit
) : SensorEventListener {

    private val sensorManager = context.getSystemService(Context.SENSOR_SERVICE) as SensorManager
    private val gyroSensor: Sensor? = sensorManager.getDefaultSensor(Sensor.TYPE_GYROSCOPE)

    val isAvailable: Boolean get() = gyroSensor != null

    /** Pixels per (rad/s) per sample at SENSOR_DELAY_GAME (~50 Hz). */
    var sensitivity: Float = 30f

    /** Angular speeds below this (rad/s) are hand tremor and ignored. */
    var deadzone: Float = 0.03f

    private var isRunning = false

    fun start() {
        if (isRunning || gyroSensor == null) return
        isRunning = true
        sensorManager.registerListener(this, gyroSensor, SensorManager.SENSOR_DELAY_GAME)
    }

    fun stop() {
        if (!isRunning) return
        isRunning = false
        sensorManager.unregisterListener(this)
    }

    override fun onSensorChanged(event: SensorEvent?) {
        if (event == null || event.sensor.type != Sensor.TYPE_GYROSCOPE) return
        val (dx, dy) = toPointerDelta(event.values[2], event.values[0], sensitivity, deadzone)
        if (dx != 0f || dy != 0f) onDelta(dx, dy)
    }

    override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) {}

    companion object {
        /** Yaw (z) turns left/right, pitch (x) tilts up/down; both inverted so the pointer follows the phone's tip. */
        fun toPointerDelta(yaw: Float, pitch: Float, sensitivity: Float, deadzone: Float): Pair<Float, Float> {
            val dx = if (abs(yaw) > deadzone) -yaw * sensitivity else 0f
            val dy = if (abs(pitch) > deadzone) -pitch * sensitivity else 0f
            return dx to dy
        }
    }
}

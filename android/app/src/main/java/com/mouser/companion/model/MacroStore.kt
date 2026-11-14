package com.mouser.companion.model

import android.content.SharedPreferences
import org.json.JSONArray
import org.json.JSONObject

object MacroStore {
    private const val PREF_KEY_MACROS = "custom_macros_json"

    fun toJson(macros: List<Macro>): String {
        val root = JSONArray()
        for (macro in macros) {
            val mObj = JSONObject()
            mObj.put("id", macro.id)
            mObj.put("label", macro.label)

            val stepsArr = JSONArray()
            for (step in macro.steps) {
                val sObj = JSONObject()
                when (step) {
                    is MacroStep.Key -> {
                        sObj.put("type", "key")
                        sObj.put("vk", step.vk.toInt())
                    }
                    is MacroStep.Combo -> {
                        sObj.put("type", "combo")
                        val vksArr = JSONArray()
                        step.vks.forEach { vksArr.put(it.toInt()) }
                        sObj.put("vks", vksArr)
                    }
                    is MacroStep.Text -> {
                        sObj.put("type", "text")
                        sObj.put("text", step.text)
                    }
                    is MacroStep.Delay -> {
                        sObj.put("type", "delay")
                        sObj.put("ms", step.ms)
                    }
                }
                stepsArr.put(sObj)
            }
            mObj.put("steps", stepsArr)
            root.put(mObj)
        }
        return root.toString()
    }

    fun fromJson(json: String): List<Macro> {
        if (json.isBlank()) return emptyList()
        val result = mutableListOf<Macro>()
        try {
            val root = JSONArray(json)
            for (i in 0 until root.length()) {
                val mObj = root.getJSONObject(i)
                val id = mObj.optString("id", java.util.UUID.randomUUID().toString())
                val label = mObj.getString("label")
                val stepsArr = mObj.optJSONArray("steps") ?: JSONArray()
                val steps = mutableListOf<MacroStep>()
                for (j in 0 until stepsArr.length()) {
                    val sObj = stepsArr.getJSONObject(j)
                    when (sObj.optString("type")) {
                        "key" -> steps.add(MacroStep.Key(sObj.getInt("vk").toShort()))
                        "combo" -> {
                            val vksArr = sObj.optJSONArray("vks") ?: JSONArray()
                            val vks = mutableListOf<Short>()
                            for (k in 0 until vksArr.length()) {
                                vks.add(vksArr.getInt(k).toShort())
                            }
                            steps.add(MacroStep.Combo(vks))
                        }
                        "text" -> steps.add(MacroStep.Text(sObj.getString("text")))
                        "delay" -> steps.add(MacroStep.Delay(sObj.getLong("ms")))
                    }
                }
                result.add(Macro(id = id, label = label, steps = steps))
            }
        } catch (_: Exception) {
            return emptyList()
        }
        return result
    }

    fun load(prefs: SharedPreferences): List<Macro> {
        val json = prefs.getString(PREF_KEY_MACROS, null)
        if (json.isNullOrBlank()) {
            return MacroRunner.defaultMacros
        }
        val parsed = fromJson(json)
        return if (parsed.isNotEmpty()) parsed else MacroRunner.defaultMacros
    }

    fun save(prefs: SharedPreferences, macros: List<Macro>) {
        val json = toJson(macros)
        prefs.edit().putString(PREF_KEY_MACROS, json).apply()
    }
}

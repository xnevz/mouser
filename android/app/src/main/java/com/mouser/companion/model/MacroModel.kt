package com.mouser.companion.model

import java.util.UUID

sealed interface MacroStep {
    data class Key(val vk: Short) : MacroStep
    data class Combo(val vks: List<Short>) : MacroStep
    data class Text(val text: String) : MacroStep
    data class Delay(val ms: Long) : MacroStep
}

data class Macro(
    val id: String = UUID.randomUUID().toString(),
    val label: String,
    val steps: List<MacroStep>
)

sealed interface MacroAction {
    data class KeyAction(val vk: Short, val isDown: Boolean) : MacroAction
    data class CharAction(val char: Char, val isDown: Boolean) : MacroAction
    data class DelayAction(val ms: Long) : MacroAction
}

object MacroRunner {
    fun expand(step: MacroStep): List<MacroAction> = when (step) {
        is MacroStep.Key -> listOf(
            MacroAction.KeyAction(step.vk, true),
            MacroAction.KeyAction(step.vk, false)
        )
        is MacroStep.Combo -> {
            val down = step.vks.map { MacroAction.KeyAction(it, true) }
            val up = step.vks.reversed().map { MacroAction.KeyAction(it, false) }
            down + up
        }
        is MacroStep.Text -> {
            step.text.flatMap { ch ->
                listOf(
                    MacroAction.CharAction(ch, true),
                    MacroAction.CharAction(ch, false)
                )
            }
        }
        is MacroStep.Delay -> listOf(MacroAction.DelayAction(step.ms))
    }

    fun expandMacro(macro: Macro): List<MacroAction> =
        macro.steps.flatMap { expand(it) }

    val defaultMacros: List<Macro> = listOf(
        Macro(
            id = "copy",
            label = "Copy",
            steps = listOf(MacroStep.Combo(listOf(VirtualKey.CONTROL, 'C'.code.toShort())))
        ),
        Macro(
            id = "paste",
            label = "Paste",
            steps = listOf(MacroStep.Combo(listOf(VirtualKey.CONTROL, 'V'.code.toShort())))
        ),
        Macro(
            id = "undo",
            label = "Undo",
            steps = listOf(MacroStep.Combo(listOf(VirtualKey.CONTROL, 'Z'.code.toShort())))
        ),
        Macro(
            id = "redo",
            label = "Redo",
            steps = listOf(MacroStep.Combo(listOf(VirtualKey.CONTROL, 'Y'.code.toShort())))
        ),
        Macro(
            id = "screenshot",
            label = "Screenshot",
            steps = listOf(MacroStep.Combo(listOf(VirtualKey.LWIN, VirtualKey.SHIFT, 'S'.code.toShort())))
        ),
        Macro(
            id = "taskview",
            label = "Task view",
            steps = listOf(MacroStep.Combo(listOf(VirtualKey.LWIN, VirtualKey.TAB)))
        ),
        Macro(
            id = "mute",
            label = "Mute",
            steps = listOf(MacroStep.Key(VirtualKey.VOLUME_MUTE))
        )
    )
}

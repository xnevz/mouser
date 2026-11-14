package com.mouser.companion

import com.mouser.companion.model.*
import org.junit.Assert.*
import org.junit.Test

class MacroTest {

    @Test
    fun keyStep_expandsToDownThenUp() {
        val step = MacroStep.Key(VirtualKey.RETURN)
        val actions = MacroRunner.expand(step)

        assertEquals(2, actions.size)
        assertEquals(MacroAction.KeyAction(VirtualKey.RETURN, true), actions[0])
        assertEquals(MacroAction.KeyAction(VirtualKey.RETURN, false), actions[1])
    }

    @Test
    fun comboStep_pressesInOrder_releasesInReverse() {
        val vks = listOf(VirtualKey.CONTROL, VirtualKey.SHIFT, 'S'.code.toShort())
        val step = MacroStep.Combo(vks)
        val actions = MacroRunner.expand(step)

        assertEquals(6, actions.size)
        // Down in order
        assertEquals(MacroAction.KeyAction(VirtualKey.CONTROL, true), actions[0])
        assertEquals(MacroAction.KeyAction(VirtualKey.SHIFT, true), actions[1])
        assertEquals(MacroAction.KeyAction('S'.code.toShort(), true), actions[2])
        // Up in reverse order
        assertEquals(MacroAction.KeyAction('S'.code.toShort(), false), actions[3])
        assertEquals(MacroAction.KeyAction(VirtualKey.SHIFT, false), actions[4])
        assertEquals(MacroAction.KeyAction(VirtualKey.CONTROL, false), actions[5])
    }

    @Test
    fun textStep_expandsToEachCharDownThenUp() {
        val step = MacroStep.Text("Hi")
        val actions = MacroRunner.expand(step)

        assertEquals(4, actions.size)
        assertEquals(MacroAction.CharAction('H', true), actions[0])
        assertEquals(MacroAction.CharAction('H', false), actions[1])
        assertEquals(MacroAction.CharAction('i', true), actions[2])
        assertEquals(MacroAction.CharAction('i', false), actions[3])
    }

    @Test
    fun delayStep_expandsToDelayAction() {
        val step = MacroStep.Delay(150L)
        val actions = MacroRunner.expand(step)

        assertEquals(1, actions.size)
        assertEquals(MacroAction.DelayAction(150L), actions[0])
    }

    @Test
    fun macroStore_roundtripsJson() {
        val macros = listOf(
            Macro(
                id = "m1",
                label = "Test Combo",
                steps = listOf(
                    MacroStep.Combo(listOf(VirtualKey.CONTROL, 'C'.code.toShort())),
                    MacroStep.Delay(50),
                    MacroStep.Text("pasted"),
                    MacroStep.Key(VirtualKey.RETURN)
                )
            )
        )

        val json = MacroStore.toJson(macros)
        val restored = MacroStore.fromJson(json)

        assertEquals(1, restored.size)
        assertEquals("m1", restored[0].id)
        assertEquals("Test Combo", restored[0].label)
        assertEquals(4, restored[0].steps.size)
        assertTrue(restored[0].steps[0] is MacroStep.Combo)
        assertTrue(restored[0].steps[1] is MacroStep.Delay)
        assertTrue(restored[0].steps[2] is MacroStep.Text)
        assertTrue(restored[0].steps[3] is MacroStep.Key)
    }

    @Test
    fun defaultMacros_containAllRequiredEntries() {
        val defaults = MacroRunner.defaultMacros
        val labels = defaults.map { it.label }
        assertTrue(labels.contains("Copy"))
        assertTrue(labels.contains("Paste"))
        assertTrue(labels.contains("Undo"))
        assertTrue(labels.contains("Redo"))
        assertTrue(labels.contains("Screenshot"))
        assertTrue(labels.contains("Task view"))
        assertTrue(labels.contains("Mute"))
    }
}

package com.mouser.companion

import com.mouser.companion.model.Action
import com.mouser.companion.model.diffToKeys
import org.junit.Assert.*
import org.junit.Test

class TypeDiffTest {

    @Test
    fun appendText_generatesOnlyCharUnits() {
        val actions = diffToKeys("hello", "hello world")
        assertEquals(6, actions.size)
        assertTrue(actions.all { it is Action.CharUnit })
        val resultString = actions.filterIsInstance<Action.CharUnit>().map { it.char }.joinToString("")
        assertEquals(" world", resultString)
    }

    @Test
    fun deleteText_generatesOnlyBackspaces() {
        val actions = diffToKeys("hello", "hel")
        assertEquals(2, actions.size)
        assertTrue(actions.all { it is Action.Backspace })
    }

    @Test
    fun replaceMiddle_generatesBackspacesThenChars() {
        val actions = diffToKeys("cat", "car")
        assertEquals(2, actions.size)
        assertEquals(Action.Backspace, actions[0])
        assertEquals(Action.CharUnit('r'), actions[1])
    }

    @Test
    fun completeReplacement_generatesFullBackspacesThenFullChars() {
        val actions = diffToKeys("abc", "xyz")
        assertEquals(6, actions.size)
        assertEquals(3, actions.count { it is Action.Backspace })
        assertEquals(3, actions.count { it is Action.CharUnit })
    }

    @Test
    fun identicalStrings_generatesNoActions() {
        val actions = diffToKeys("identical", "identical")
        assertTrue(actions.isEmpty())
    }

    @Test
    fun emojiSurrogatePairs_doesNotSplitSurrogate() {
        // 🎉 is \uD83C\uDF89, 🎊 is \uD83C\uDF8A
        val old = "Party \uD83C\uDF89"
        val new = "Party \uD83C\uDF8A"
        val actions = diffToKeys(old, new)

        // Both units of the surrogate pair must be deleted and both replaced
        val backspaces = actions.filterIsInstance<Action.Backspace>()
        val chars = actions.filterIsInstance<Action.CharUnit>()
        assertEquals(2, backspaces.size)
        assertEquals(2, chars.size)
        assertEquals('\uD83C', chars[0].char)
        assertEquals('\uDF8A', chars[1].char)
    }
}

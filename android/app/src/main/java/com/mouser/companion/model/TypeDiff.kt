package com.mouser.companion.model

sealed interface Action {
    data object Backspace : Action
    data class CharUnit(val char: Char) : Action
}

/**
 * Calculates the sequence of actions (backspaces followed by char units)
 * needed to transform `old` into `new`.
 * Preserves UTF-16 surrogate pairs so that half an emoji or supplementary code point
 * is never left as a common prefix.
 */
fun diffToKeys(old: String, new: String): List<Action> {
    var commonPrefix = 0
    val maxPrefix = minOf(old.length, new.length)
    while (commonPrefix < maxPrefix && old[commonPrefix] == new[commonPrefix]) {
        commonPrefix++
    }

    // If the common prefix ends on a high surrogate, do not split the surrogate pair.
    if (commonPrefix > 0 && Character.isHighSurrogate(old[commonPrefix - 1])) {
        commonPrefix--
    }

    val actions = mutableListOf<Action>()

    // Delete characters from old that are past the common prefix
    val toDelete = old.length - commonPrefix
    repeat(toDelete) {
        actions.add(Action.Backspace)
    }

    // Insert characters from new that are past the common prefix
    for (i in commonPrefix until new.length) {
        actions.add(Action.CharUnit(new[i]))
    }

    return actions
}

package com.mouser.companion

import androidx.compose.ui.test.*
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Instrumented UI smoke test running on Android emulator / device.
 * Verifies core UI hierarchy, navigation tabs, and trackpad surface rendering.
 */
@RunWith(AndroidJUnit4::class)
class CompanionUiSmokeTest {

    @get:Rule
    val composeTestRule = createAndroidComposeRule<MainActivity>()

    @Test
    fun appLaunches_displaysTopBarAndBottomNavigation() {
        // Verify Mouser title in top connection bar
        composeTestRule.onNodeWithText("Mouser").assertIsDisplayed()

        // Verify bottom navigation tab labels
        composeTestRule.onNodeWithText("Trackpad").assertIsDisplayed()
        composeTestRule.onNodeWithText("Air Mouse").assertIsDisplayed()
        composeTestRule.onNodeWithText("Slides").assertIsDisplayed()
        composeTestRule.onNodeWithText("Keys").assertIsDisplayed()
        composeTestRule.onNodeWithText("Type").assertIsDisplayed()
    }

    @Test
    fun navigationBetweenTabs_switchesContentCorrectly() {
        // Navigate to Slides tab
        composeTestRule.onNodeWithText("Slides").performClick()
        composeTestRule.waitForIdle()
        composeTestRule.onNodeWithText("Start (F5)").assertIsDisplayed()
        composeTestRule.onNodeWithText("HOLD FOR LASER").assertIsDisplayed()

        // Navigate to Keys / Macros tab
        composeTestRule.onNodeWithText("Keys").performClick()
        composeTestRule.waitForIdle()

        // Navigate to Type tab
        composeTestRule.onNodeWithText("Type").performClick()
        composeTestRule.waitForIdle()

        // Return to Trackpad tab
        composeTestRule.onNodeWithText("Trackpad").performClick()
        composeTestRule.waitForIdle()
        composeTestRule.onNodeWithContentDescription("Trackpad multi-touch gesture surface").assertIsDisplayed()
    }
}

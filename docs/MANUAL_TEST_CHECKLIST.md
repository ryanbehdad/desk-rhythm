# Manual test checklist: three monitors

Set-up: two external monitors and the laptop display. If possible, use different scaling (for example 100 %, 100 %, 150 %) and put one monitor **left of or above** the primary display (negative coordinates).

Use `DeskRhythm.exe --test-mode` for fast checks (10 s / 5 s / 3 s phases, 1 "minute" = 1 second, 10 s time-out). Do the final check in normal mode.

Mark each line ✅ or ❌.

## Start-up

- [ ] 1. Double-click the exe. A tray icon appears. There is no console, no main window and no taskbar button.
- [ ] 2. A "Ready when you are" card shows on **all three** displays.
- [ ] 3. Start the exe again. No second icon appears. The status panel of the first copy opens.

## Reminder on all monitors

- [ ] 4. Press **Start sitting**. All cards close. The tooltip shows "Sitting: 0:10 remaining" (test mode).
- [ ] 5. Click in Notepad and type. When the phase ends, **one** "Time to stand" card shows on **each** display, in the bottom-right corner, above the taskbar.
- [ ] 6. Your typing continues in Notepad. The cards do not take keyboard focus.
- [ ] 7. On the 150 % display, the card has the same physical size as the text around it (not tiny, not blurry).
- [ ] 8. Click **I'm standing** on the **second** (non-primary) monitor. **All** cards close at once. The standing countdown starts at that moment.

## Snooze, skip, time-out

- [ ] 9. At "Time to move", press **More → Snooze 10 min** (10 s in test mode). All cards close. After exactly that delay, the **same** reminder ("Time to move") returns **once** on all displays.
- [ ] 10. Press **Start walk**, then at "Ready to sit?" press **I'm sitting**. The routine repeats from Sitting.
- [ ] 11. At "Time to stand", do nothing. The cards hide after 30 s (10 s in test mode). The tray shows "Action due". After 5 min (5 s), the reminder returns once. No duplicate cards.
- [ ] 12. At "Time to stand", press **More → Skip this standing break**. A new sitting phase starts.
- [ ] 13. Press **×** or Esc on a card. All copies close. The reminder returns later. The phase does not change.
- [ ] 14. Point to a card: it does not time out while the pointer is on it or the **More** menu is open.

## Tray and status panel

- [ ] 15. Left-click the tray icon: the status panel opens on the display with the mouse. Click it again: it closes.
- [ ] 16. Right-click the tray icon: the menu has Show status, Pause, Pause 30 min / 1 hour / 2 hours, Quiet mode, Switch phase, Stop routine, Settings, Test reminder, Exit.
- [ ] 17. Close the status panel and the settings window. The app continues in the tray.
- [ ] 18. **Switch phase → I'm standing now**: a full standing phase starts at once.

## Pause and quiet mode

- [ ] 19. **Pause 30 min** (30 s): no card shows during the pause. The tooltip shows "Paused until …". After the pause, the countdown continues.
- [ ] 20. **Quiet mode**: no cards until you turn it off. Then the countdown continues.
- [ ] 21. During a pause, **Resume** and **Stop** work from the status panel.

## Lock and sleep

- [ ] 22. Lock Windows (Win+L) for less than 5 minutes, then unlock. The countdown continues. No card.
- [ ] 23. Lock Windows for more than 5 minutes (or put the PC to sleep). After you unlock, **one** "Welcome back" card shows with **Resume** and **Restart cycle**. There is no backlog of reminders.

## Display changes

- [ ] 24. While cards show, unplug one monitor. No crash. The cards are rebuilt on the remaining displays. No window is lost off-screen.
- [ ] 25. Plug it back in and press **Test reminder**. Each display shows "Display n of 3 · scale %".
- [ ] 26. Change the scale of one display (Settings → Display) and press **Test reminder**. The cards are sharp and in the corner.

## Settings and persistence

- [ ] 27. Change sitting to 25 min, monitors to **Primary monitor**, corner to **Top right**. Exit and start again. The settings are kept.
- [ ] 28. With **Primary monitor**, a reminder shows only on the primary display. With **Monitor with mouse**, only on the display with the mouse.
- [ ] 29. Turn on **Launch at Windows sign-in**. Sign out and sign in. The app starts. Turn it off: Task Manager → Startup apps no longer shows it. No admin prompt at any step.
- [ ] 29a. On the "Ready when you are" card, turn on **Start with Windows** on one display. The switch turns on at once on the cards of the **other** displays too. Right-click the tray icon: **Start with Windows** has a check mark.
- [ ] 29b. Click **Start with Windows** in the tray menu. The check mark goes away, and the switch in an open Settings window turns off too.
- [ ] 30. Exit while sitting with 12 min left. Start again: a card asks to **Resume** (sitting, 12 min) or **Restart cycle**.
- [ ] 31. **Reset settings** brings back the defaults.

## Theme and accessibility

- [ ] 32. Switch Windows between light and dark mode. Cards, panel and menus follow the mode.
- [ ] 33. Click a card, then use Tab and Enter: buttons have a clear focus ring and work with the keyboard.

## Uninstall

- [ ] 35. Turn on **Launch at Windows sign-in**, then Settings → **Uninstall…** → **No**. Nothing changes.
- [ ] 36. **Uninstall…** → **Yes**. The app closes, File Explorer opens with `DeskRhythm.exe` selected, `%LOCALAPPDATA%\DeskRhythm` is gone, and Task Manager → Startup apps no longer lists Desk Rhythm.

## All day

- [ ] 34. Leave the app running for a workday. Task Manager shows near 0 % CPU and about 30–60 MB memory.

## No standing desk

Note: test mode uses the real `settings.json`, so a desk change made in test mode is kept. **Reset settings** turns the standing desk on again.

- [ ] 37. Right-click tray → turn off **I have a standing desk**. The status panel shows two "I'm now…" buttons (Sitting, Moving) and the link **I have a standing desk here**. Settings shows **No standing desk** with the Sitting and Walking break sliders (40 / 5 min).
- [ ] 38. Start sitting. At the end (10 s in test mode) the card says **Time for a break**, "Stand up and walk for …", with **Start break**. The tray tooltip says "Action due: time for a break".
- [ ] 39. Press **Start break**. The break lasts 4 s (test mode). Then **Ready to sit?** → **I'm sitting** starts a new sitting phase.
- [ ] 40. At **Time for a break**, press **More → Skip this break**. A new sitting phase starts.
- [ ] 41. While a **Time to stand** card shows (standing desk on), turn the desk off in the tray. The card changes to **Time for a break** at once, without a second chime.
- [ ] 42. With the routine stopped, the Ready card has **More → No standing desk here**. Choose it: the card stays and its text talks about a walk. The "I'm standing" button is gone.
- [ ] 43. On a **Welcome back** card, **More → No standing desk here** (or **I have a standing desk here**) changes the mode and starts a fresh sitting phase.
- [ ] 44. Change the sitting-only durations, switch the desk on and off, exit and start again. Both routines keep their own durations.

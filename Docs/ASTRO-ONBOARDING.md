# ASTRO Onboarding and Accessibility

The Unity bootstrap scene opens with a welcome screen, accessibility setup, six safe practice activities, a completion screen, and the existing First Light mission board. Practice interactions use separate communications and solar props; they do not change the mission resources or objectives.

## Player options

- English, isiZulu, and Afrikaans interface and tutorial copy
- Voice, visual, or combined guidance; selectable hint frequency
- No-reading mode with large activity icons, world arrows, and extra hints
- Adjustable text size, high contrast, subtitles, voice volume, and click-to-walk
- Remappable movement and interact keys; arrow keys also work for movement
- Skip, repeat, go back, replay, and continue without the tutorial

In the final solar practice activity, the main button repeats ASTRO's hint. Walk to the practice solar panel and press the interact key to complete the activity and open the completion screen.

Options and key bindings are stored in PlayerPrefs on the local computer. They are not sent to a service.

## ASTRO audio

Audio clips load from `Assets/Resources/Audio/Astro/<language>/`. English sample narration is included in `english/`. Add native recordings with the same clip names in `isizulu/` and `afrikaans/` to provide speech in those languages. If a matching native clip is missing, ASTRA shows the localized line as a caption, so voice-guided players can still follow the lesson.

The clip names used by the tutorial are `welcome`, `settings`, `meet`, `move`, `look`, `interact`, `equipment`, `equipment_0` through `equipment_4`, `practice`, `ready`, `success`, `hint_move`, `hint_look`, and `hint_interact`.

## Regenerate the scene

Open the project in Unity 6.6 and select **ASTRA → Build First Playable Scene**. The builder regenerates `Assets/_Project/Scenes/ASTRA_Bootstrap.unity`, includes the practice deck and ASTRO companion, and keeps that scene enabled in Build Settings.

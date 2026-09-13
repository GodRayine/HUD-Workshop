# HUD Workshop

<img src="images/icon.png" alt="HUD Workshop" width="96" height="96">

**English** · [Русский](README.ru.md)

Edit the FFXIV HUD directly in game: move and align elements, adjust their properties and share layouts.

## Installation and updates

Requires Windows, Dalamud **15.0.3.4 / API 15** and FFXIV **2026.09.01.0000.0000**. Penumbra is not required. The plugin interface is in Russian.

1. Open `/xlsettings` → Experimental → Custom Plugin Repositories.
2. Add this address:

   ```text
   https://raw.githubusercontent.com/GodRayine/HUD-Workshop/main/repo.json
   ```

3. Open `/xlplugins`, refresh the list and install **HUD Workshop**.
4. Enter `/hudworkshop` to open the editor.

Install updates through `/xlplugins`. The [1.1 download](https://github.com/GodRayine/HUD-Workshop/releases/tag/v1.1.0) is also available on the release page.

If switching from a development build, save the HUD, disable that build and remove its Dev Plugin Locations entry first. Keep the HudEditor configuration and run only one copy.

## Moving elements and changing properties

- Click an element frame and drag it. Its properties appear in the editor; you can also select the element from the list.
- Enable **Привязка к сетке** (snap to grid) and **Приклеивать панели** (snap panels) to position elements. **Показывать сетку** controls grid visibility.
- Hold **Alt** to temporarily disable snapping.
- Adjust position, scale and visibility in the properties. Standard hotbars also support shape changes.
- **Элементы других режимов** shows available frames for inactive elements.

## Editing several elements

**Shift + click** adds or removes an element from the selection. Use marquee selection and groups to edit several elements together. Moving a selection preserves its relative positions. The selection section offers alignment, equal spacing and **Выровнять по сетке** (align to grid).

## Saving and undoing changes

- **Отменить шаг** (undo) and **Повторить** (redo) navigate the change history.
- **Отменить всё** (cancel all) restores the original preview state.
- **Сохранить HUD** saves changes.
- Closing the editor cancels unsaved changes. **Esc** cancels an active drag; otherwise it closes the window.

If **Повторить отмену** (retry cancellation) appears, click it to retry restoring the layout. New editing is unavailable until recovery completes.

## Exporting and importing layouts

To export, click **Экспортировать и скопировать** (export and copy) in the layout sharing section and send the resulting string to another player.

To import:

1. Copy a layout string and click **Вставить из буфера** (paste from clipboard), or paste it into the text field.
2. Optionally enable **Подстроить координаты под разрешение** to adapt positions to your resolution; this does not change scale.
3. Click **Проверить строку** (validate string) and read the result.
4. Click **Применить предпросмотр** (apply preview), inspect the layout and click **Сохранить HUD** to keep it. Use cancellation to discard it.

Codes transfer supported positions, scales, visibility and element shapes. Action assignments, editor groups and combined/split target or status preferences are not transferred. Unsupported elements are skipped.

## Usage notes

Editing pauses during combat and transitions. Cross hotbars are unsupported. Chat hiding applies while the plugin is loaded. When all status elements are hidden and their mode has not yet been determined, the editor assumes split mode.

If the window does not open, check that HUD Workshop is enabled in `/xlplugins` and that your game and Dalamud versions match the requirements above.

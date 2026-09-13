# Release procedure / Порядок выпуска

1. Complete the pending in-game checks in `review/TEST-MATRIX.md`; record the exact build and result. / Завершить игровые проверки и записать сборку и результат.
2. Build and run `package-release.ps1` from PowerShell 7 with the pinned SDK and Dalamud references. It derives filenames from `plugin/HudEditor.csproj`. / Собрать комплект с зафиксированными зависимостями; версия берётся из проекта.
3. Inspect the plugin/source archives and `SHA256SUMS.txt`. Keep matching source available under GPL-3.0-only. / Проверить архивы и хеши, предоставить соответствующие исходники по GPL-3.0-only.
4. Publication requires the owner's separate instruction. Upload the matching source commit and release archives only then. / Публикация — только по отдельному указанию владельца.
5. After verifying anonymous HTTPS downloads, generate a catalog with `make-repository.ps1 -PackageUrl <ZIP HTTPS URL> -ProjectUrl <repository HTTPS URL> -ManifestPath <built HudEditor.json> -OutputPath <new catalog path>`. Verify it before replacing `repo.json`. / Проверить анонимные загрузки, создать и проверить новый каталог перед заменой старого.
6. Verify clean installation/update. For official testing submission use the draft manifest and `images/icon.png`, with a verified public source commit. / Проверить установку и обновление; для официальной заявки использовать проверенный публичный коммит и иконку.

The checked-in `repo.json` intentionally points to published 1.0.0 until a later release is authorized. The local 1.1 kit is a review candidate, not proof of acceptance. / Текущий `repo.json` намеренно указывает на опубликованную 1.0.0. Локальный комплект 1.1 предназначен для ревью.

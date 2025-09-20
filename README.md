# Code Prompt Builder

A Windows Forms utility (targeting .NET 9) that lets you interactively assemble a single, clean prompt containing:

- An optional System Prompt
- An optional User Prompt
- The selected source files from a directory tree (auto?filtered by patterns and respecting .gitignore)

It is designed to streamline creating rich AI / LLM context prompts from local source code without manually copying files.

## Key Features

- Interactive Tree View of your source directory
  - Automatically hides entries ignored by any nested `.gitignore` files
  - Skips dot?prefixed directories (e.g. `.git`, `.vs`, `.idea`)
  - Remembers which nodes were collapsed
- Checkable file nodes (directories are informational only and cannot be checked to avoid accidental bulk dumps)
- Extension pattern filter (e.g. `*.cs; *.json; *.md;`)
  - Use the Select button to auto?check all currently visible files matching patterns
- Persistent settings stored in a lightweight JSON file named `.CodePromptBuilder` placed alongside the executable and current working directory
  - Remembers: source directory, extension patterns, prompts, checked files, collapsed folders, which prompts are enabled
- Prompt composition preview (read?only) with immediate Clipboard copy on Generate
- Uses fenced code blocks for each included file:
  ```
  ```path/to/File.cs
  // file content ...
  ```
  ```
- Graceful error handling for unreadable files or directories
- Supports multi?selection accumulation—add/remove checks and regenerate instantly

## How It Works

1. Choose (or auto?load) the source directory.
2. (Optional) Refine file extension patterns (semicolon?separated; spaces allowed). Example: `*.cs; *.csproj; *.sln; *.json; *.xml; *.md;`.
3. Use Select to mass?check files matching patterns, or manually check individual files.
4. Enter / enable the System and/or User prompts.
5. Click Generate:
   - The composite prompt (system + user + content section) is rendered to the preview box.
   - All chosen files are appended under a `--- CONTENT ---` marker.
   - Each file is wrapped in a fenced block labeled with its full path for clarity.
   - The full prompt text is copied automatically to the Clipboard.

## Settings Persistence

Settings are serialized to `.CodePromptBuilder` (JSON). The loader searches candidate directories in order:
1. Preferred / user?selected directory
2. Current working directory
3. Executable directory

The following fields are stored:
- SourceDirectory
- FileExtensions
- UserPrompt / SystemPrompt
- IsSystemPromptEnabled / IsUserPromptEnabled
- CheckedPaths (only files)
- CollapsedPaths (directories)

## .gitignore Awareness

The app parses every `.gitignore` under the selected root (depth?ordered) and builds an evaluation list. A file or directory that would be ignored by Git is hidden from the tree (unless explicitly unignored by a later negation rule). Simplifications:
- Standard wildcard / character class support (`*`, `**`, `?`, `[abc]`, `[!abc]`)
- Directory targeting via trailing slash in patterns
- Negation via leading `!`

## File Inclusion Logic

Only explicitly checked file nodes are included. (Directories are not checkable.) For each checked directory path variant (rare—normally not checkable), the code defensively enumerates its immediate children; but by design the UI prevents directory checking.

Extension patterns are applied when using the Select button and again during generation if directory enumeration occurs.

## UI Notes

- Font choices: `Consolas` for tree and preview (code?centric), `Segoe UI` for text inputs.
- Status bar conveys operation results (loaded directory, selection counts, success/error messages).
- Window is resizable (min size enforced) and uses table layouts for proportional scaling.

## Requirements

- Windows (WinForms)
- .NET 9 SDK (preview / latest as applicable)

## Building

Using the .NET CLI:

```
 dotnet build
 dotnet run
```

Or open the solution in Visual Studio 2022 (or newer supporting .NET 9) and run the WinForms project.

## Typical Use Case

You are preparing a multi?file context for an AI code assistant (e.g., a refactor request). Instead of manually copying each file:
- Launch Code Prompt Builder
- Point to the repository root
- Filter to `*.cs; *.csproj; *.sln; *.json; *.md;`
- Auto?select matching files
- Add a System Prompt with coding conventions, plus a User Prompt describing the task
- Generate and paste into your AI tool

## Limitations / Future Ideas

Potential enhancements (not yet implemented):
- Dark theme / theming support
- Async file loading for very large repositories
- Size / token estimation (e.g., OpenAI/Anthropic token heuristics)
- Export prompt to file instead of only Clipboard
- Inclusion of diff / patch contexts
- Multi?root workspace support
- Drag & drop folder selection

## Contributing

Issues and pull requests are welcome. Please keep contributions focused and well?scoped (one feature or fix per PR). For substantial changes, open an issue first to discuss direction.

Suggested contribution workflow:
1. Fork
2. Create feature branch
3. Commit with clear messages
4. Open PR describing motivation & behavior

## License

MIT License (you may adjust if you choose a different license). Be sure to add a `LICENSE` file if publishing publicly.

## Security / Privacy

- No network calls are made by the application.
- All processing stays local; only the Clipboard is written.
- Settings file may contain prompt text—avoid storing secrets.

## Troubleshooting

- Nothing appears in the tree: ensure the selected directory exists and you have read permission.
- Files missing: they may be excluded by `.gitignore` or start with a dot directory segment.
- Clipboard not updated: some clipboard managers/security tools can block access—retry or run elevated if necessary.
- Build errors: verify you have the .NET 9 SDK installed (`dotnet --info`).

## Acknowledgments

Developed to streamline creating rich AI prompt contexts from real project codebases.

---
Happy prompting!

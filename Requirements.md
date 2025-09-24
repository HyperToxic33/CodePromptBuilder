Core Functionality
UI Controls:

Inputs: Text inputs for a Source Directory (with a "Browse" button) and semicolon-delimited File Extensions.

Selection: A checkbox tree view recursively showing the source directory's contents, with a "Clear" button.

Prompts: Separate multi-line text areas for a "System Prompt" and "User Prompt," each toggled by a checkbox.

Output: A "Generate & Copy" button, a read-only preview area, and a status bar for feedback.

File Selection Rules:

Include any individually checked file, regardless of its extension.

For any checked folder, include all immediate child files that match the extension patterns.

Generation Logic:

Concatenate enabled prompts and selected file content to the clipboard and preview area.

Order: System Prompt, User Prompt, then a --- CONTENT --- section.

Formatting: Wrap each file's content in a markdown code block tagged with its full path.

Settings Persistence:

On exit, automatically save all user inputs, UI layout, and all checkbox states (prompts and tree view selections) to a JSON file.

Restore all saved settings on application startup.

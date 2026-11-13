# Custom Versification Editor

*This page is the English user guide, [CustomVrsEditor_UserGuide.rtf](CustomVrsEditor_UserGuide.rtf). The guide is also available in Spanish ([CustomVrsEditor_UserGuide_sp.rtf](CustomVrsEditor_UserGuide_sp.rtf)) and French ([CustomVrsEditor_UserGuide_fr.rtf](CustomVrsEditor_UserGuide_fr.rtf)).*

This tool creates and edits a Paratext project's *custom.vrs* file — the file that records how a project's versification (chapter and verse numbering) differs from a standard base versification. It is available as a standalone program, as a Paratext plugin on the Tools menu, and as a tool built into Paratext's Checks menu.

## 1. Opening the tool

**From Paratext (plugin):** open the project you want to work on, then choose Tools > Custom Versification Editor... The tool opens that project's custom.vrs file, with its menus in the same language as Paratext's (English if Paratext uses a language the tool does not have). If the project does not have a custom.vrs file yet, the tool opens with everything empty, and File > Save creates the file in the project's folder. The plugin must be installed once first (see below).

**From Paratext (Checks menu):** If you installed the .cms version, open the project you want to work on, then choose Checks > Versification > Custom Versification Editor. Paratext launches the tool automatically, already pointed at that project's custom.vrs file. If the project does not have a custom.vrs file yet, the tool opens with everything empty; nothing is written to disk until you save.

**Installing the plugin:** download Install_Phils_tools.exe from the Paratext_Plugins folder (if your browser warns about the file, choose to keep it) and close Paratext. Double-click Install_Phils_tools.exe. If Windows says it protected your PC, click “More info”, then “Run anyway” (the installer is not signed yet). Tick Custom Versification Editor and click “Install / update ticked”. If Windows asks for permission, click Yes. The installer puts the plugin into every Paratext 9 installation on the computer, in plugins\CustomVrsEditor, and checks that Paratext can load it. The same window later shows “Update available” when there is a newer version, updates the plugin, removes it (“Remove ticked”), and opens this guide (“User guide”). The plugin was built for Paratext 9.5 and may not load in older versions; the Checks-menu route works in any Paratext 9.

## 2. The project bar

The blue bar under the menu shows which project is open and, when it can determine it, the project's base versification, for example:

```
Project: PDT15 uses the English (eng.vrs) versification
```

The versification name is read from the project's Settings.xml file and is always shown in English, even if you switch the tool's interface language. If Settings.xml cannot be found or read, the bar simply shows the project name on its own.

The yellow banner below it is a reminder: after saving changes to custom.vrs, restart Paratext so it picks up the new versification data.

## 3. The three tabs

A custom.vrs file has three kinds of entries, each edited on its own tab. In every tab you add an entry by filling in the boxes at the bottom and clicking Accept (or pressing Enter in any box); you remove an entry by selecting its row and clicking Delete or pressing the Delete key. Every entry has a Disabled checkbox — checking it comments the line out (prefixes it with #) instead of deleting it, so you can turn it off without losing it. Entries are always resorted into standard Bible book order automatically, no matter what order you add them in.

### Chapter/Verse Differences

Lists books whose chapters have a different number of verses than the base versification. Pick a Book, type a Chapter number and its Max verse count, then click Accept. Adding another chapter for a book you already listed merges into that book's existing row instead of creating a duplicate.

### Mapping

Lists verses that map to a different reference in the base versification. Fill in Book/Chapter/Verse for From and again for To, then click Accept. Leave the small "verse end" box after a Verse box blank for a single verse, or fill it in to create a range (for example Verse 16, verse end 18 produces 21:16-18).

### Missing Verses

Lists verses that do not exist in this project's Bible text (the tool adds the leading "-" automatically). Fill in Book/Chapter/Verse and click Accept. To add several verses in a row at once, also fill in the small "verse end" box after Verse (for example Chapter 11, Verse 1, verse end 12 adds EXO 11:1 through EXO 11:12). Each verse in the range is listed as its own entry, as custom.vrs requires; verses already in the list are skipped.

In every tab, the Book box is a combo box: pick a book from the dropdown, or simply type a code if it is not in the list (useful for less common or legacy book codes).

## 4. What a saved file looks like

Saving writes a plain-text custom.vrs file with the three sections in order, each with its standard header, for example:

```
# Chapters with different number of verses than Original
GEN 1:32 2:24
# Mapping
EXO 21:16 = EXO 21:17
# Missing verses
-MRK 7:16
```

A section is only written if it has at least one entry, and a disabled entry keeps its data but gains a leading # so Paratext ignores it, for example `#-MRK 7:16`.

## 5. Saving and file locations

Use File > Save (Ctrl+S) to save to the current file, or File > Save As (Ctrl+Shift+S) to choose a new location. A project's custom.vrs file belongs directly in that project's folder, next to its Settings.xml, so Paratext will find it. After saving, restart Paratext so the change takes effect.

## 6. Changing the interface language

The Language dropdown near the top switches the tool's own menus, labels, and messages between English, Spanish, French, Portuguese, and Chinese (Simplified). When opened from the Paratext plugin, the tool starts in the same language as Paratext's menus. Versification names (like "English (eng.vrs)") are never translated, since they refer to fixed file names Paratext itself uses.

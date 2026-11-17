# Changelog

## 1.2 (17/11/2026)

- The summaries with Ollama are gone: the program no longer looks for Ollama and Settings no longer asks for a model.
- The Claude Code package is replaced by a plain text export: the **Export to a text file** button writes one `.txt` per repo in `exports\`, with the texts to work on and the recurring errors, for you to read. Keys are still masked and hidden characters shown. Nothing is copied to the clipboard.
- Repos that are gone can be taken off the list from where you look: right-click the repo or the **Gone** group on the left, the Delete key, the **Manage** tab and **Overview**. It always asks first, and nothing changes on GitHub. A repo you delete from the program ends up in **Gone** and says so.
- Settings explains that texts with links to sites outside the trusted list wait in **Quarantine** until you approve them.
- Code base rewritten in English and cleaned up.
- Your data carries over: what 1.0 and 1.1 saved is read when the new files are missing, and the old files are never changed. 1.2 saves under English file names next to them.
- Releases 1.0 and 1.1 were withdrawn.

## 1.1 (03/11/2026)

- New "How it is going" panel at the top of every repo page: views, visitors, clones, downloads, stars, forks and watchers, with day-by-day charts, where visitors come from and the most viewed pages.
- The table of all repos shows who sees each repo, version, views, downloads, stars, reports to read and errors. License, people and commits since the release are on the repo page.
- The numbers stay on the computer after the 14 days GitHub keeps them. A number that cannot be read says "not available" and why, never 0.
- If the history file is locked by another program, old days are not lost: they come back in as soon as the program is reopened.
- Repos that disappeared sit in a closed group at the bottom and can be removed from the list, one or all. Nothing changes on GitHub, and a repo that comes back is picked up again.
- The left column keeps its position when the missing repos are opened and after Refresh.
- Update notice: 20 seconds after start the program asks GitHub, without a key, whether a newer version exists. It never downloads or installs anything, it can be turned off in Settings and checked by hand.
- The notice window always shows the SHA-256 fingerprint of the file above the notes, visible without scrolling and selectable. If file and notes do not match it says in red not to use the file; if the fingerprint is missing it says to look at it on the release page.
- The notes in the notice window are clean text, and its button only opens the version page on the official GitHub.
- If the language is changed in Settings while the notice is waiting, the notice shows up on the new window.
- After "Make public" the publication of a release retries by itself without blocking the window, and the final message says what really happened.
- Cancel during the publication stops the writing up to the last useful moment. After an attempt the window reads the release again and says whether it is still a draft, published, or unknown, and the Publish button can be used again.
- If the release was deleted by someone else, the window notices after a single wait and says so instead of going through all the attempts.
- The GitHub page in 5 languages has the link to the website at the top, the "Update" and "How to check the file" sections, says that the window shows the fingerprint, and has a new picture with sample data.

## 1.0 (03/11/2026)

- Stricter checks before a repo is made public: the program looks for passwords and keys also in the history, tags, pull requests, commit messages, reports, comments and release notes. If it finds something it stops and says where, without showing the value. The check helps but does not find everything.
- If a repo is too big to be checked completely, it is not switched to public and the program points to the website. While checking it shows its progress and can be cancelled.
- Commits made by the program no longer show the real email. Correcting an old release no longer moves the "Latest" mark. A name that redirects to a renamed repo is stopped. The Website field is available.
- The header shows "Personal license" or "Commercial license" with the holder's name. The program does everything with or without a license and does not use the network to check it.
- Project Manager speaks five languages: Italian, English, French, Spanish and German. The language is chosen at the first start and can be changed in Settings; the window reopens at once in the new language.
- Confirmations that ask to type the repo name work the same way in all languages.
- The check of incoming texts also recognises, in French, Spanish and German, sentences addressed to an AI and dangerous requests. It helps but does not find everything.
- The license window shows the shop address in your language.
- The GitHub page of the program is in English, Italian, French, Spanish and German, with the real names of buttons and tabs in each language.

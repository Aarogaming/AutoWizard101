# Optional game sign-in setup

Maelstrom can store a Wizard101 sign-in in **Windows Credential Manager** for
the current Windows user on this PC. Open the game sign-in settings, enter the
username and password locally, and choose **Save sign-in on this PC**. The
settings page never fills an existing username or password back into its fields.
To replace a sign-in, enter both again. **Remove saved sign-in** deletes only
Maelstrom's entry for that game.

**This is credential setup, not automatic sign-in.** No login submission,
launcher interaction, reconnect loop or scheduled login is connected yet. Saving
credentials does not launch Wizard101, change native Idle, or start a routine.
The current application settings link is integrated by the coordinating task.

## Storage and privacy

- The entry is `Maelstrom/GameSignIn/v1/wizard101`, a generic credential with
  Windows local-machine persistence for the current user. It is not stored in
  the project, packs, reports, application preferences or environment files.
- Enter the credentials once in the application. Do not put them in chat,
  `.env`, command-line arguments or a game-pack export. There is no credential
  CLI/API/MCP export or password retrieval method in this increment.
- Newly entered passwords are masked. Both fields clear after Save, Remove or
  closing. The application reports only missing/stored/unavailable status, never
  a saved username or password.
- Temporary unmanaged buffers used for saving are zeroed before freeing, even
  after a failed write. Existence checks examine exact-target metadata only;
  native password copies returned by Windows are cleared before freeing and are
  never copied into managed objects or shown in the UI.
- Windows protects the saved entry for the Windows account. This does not make
  it inaccessible to every program running as that account. WinForms text
  controls briefly use managed strings during entry; clearing a field cannot
  guarantee immediate erasure of every managed-memory copy.
- The entry belongs to the Windows vault, independently of the application
  directory. Updating or removing application files does not remove it. Use
  **Remove saved sign-in**, or Windows Credential Manager, to delete it.

This dialog never requests capture or logging. Avoid recording the credential
settings screen with external recording tools while entering details.

## Integration contract

`GameCredentialProfile` contains nonsecret profile metadata. The initial profile
is Wizard101; other game profiles can use distinct safe identifiers without
sharing the credential entry.

`GameCredentialStore` exposes `Windows()`, `GetStatus(profile)`,
`Save(profile, userName, ReadOnlySpan<char> password)` and `Remove(profile)`.
It has no password-returning method. `GetStatus` confirms entry existence,
not password validity or login readiness. Native calls are injectable in unit
tests, which use only synthetic memory buffers and never modify the real vault.

A future login adapter needs separate qualification: a narrowly scoped secret
lifetime, exact launcher/client identity, positively recognized login fields,
explicit opt-in to submit, one bounded attempt under shared ownership, no capture
or input transcript containing credentials, cancellation/user takeover,
verification after submission, and stop conditions for invalid credentials,
challenges or changed screens. It must not infer permission to log in from the
mere presence of a saved entry. No game-client memory login method or network
manipulation is provided here.

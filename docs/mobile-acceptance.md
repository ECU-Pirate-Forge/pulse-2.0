# Mobile Acceptance Matrix

Use a physical device through the documented HTTPS dev tunnel. Test with a
fresh survey and trivia game; do not enter real student information. Record
device/OS/browser versions and pass/fail in the final two columns.

Baseline viewport for desktop emulation is `390 × 844`, but QR and reconnect
rows require a physical phone.

| Area | iOS Safari | iOS Chrome | Android Chrome | Android Firefox | Expected result | Result / notes |
|---|---:|---:|---:|---:|---|---|
| Open home and join by six-character code | Required | Required | Required | Recommended | No horizontal overflow; invalid code error is announced and valid code opens the survey | |
| Scan instructor QR | Required | Recommended | Required | Recommended | Camera opens the public HTTPS `/join/{code}` URL; no localhost, API, Admin, login prompt, or certificate warning. The one-time Microsoft Dev Tunnels anti-phishing/trust screen is expected and may be acknowledged. | |
| Portrait survey (`390 × 844`) | Required | Required | Required | Recommended | Every question and the submit control remain readable and operable | |
| Landscape survey | Required | Recommended | Required | Recommended | Rotation retains selections and controls are not clipped | |
| On-screen keyboard | Required | Required | Required | Recommended | Join-code/name inputs remain visible; keyboard dismissal does not submit or erase input | |
| Submit all survey question types | Required | Required | Required | Recommended | Multiple choice, multiple answer, true/false, and matching submit once; confirmation replaces the form | |
| Survey reconnect | Required | Recommended | Required | Recommended | Brief Wi-Fi loss shows reconnect UI; restoring the network resumes the page without duplicate submission | |
| Survey reveal fallback | Required | Recommended | Required | Recommended | After SignalR interruption, polling eventually displays revealed aggregate results | |
| Join individual trivia and answer | Required | Required | Required | Recommended | Open question appears, one answer is accepted, points display, and leaderboard refreshes | |
| Join team trivia | Required | Recommended | Required | Recommended | Team is required, chosen team is retained, and team leaderboard displays | |
| Trivia reconnect/polling | Required | Recommended | Required | Recommended | After a brief network loss, the next successful poll restores current question and leaderboard state | |
| Slow network | Required | Recommended | Required | Recommended | With browser throttling or constrained network, loading/submitting remains understandable and repeated taps do not create duplicate answers | |
| Text zoom and orientation | Required | Recommended | Required | Recommended | At 200% text zoom in both orientations, content reflows without loss of controls | |
| Screen reader and keyboard | VoiceOver | VoiceOver | TalkBack | TalkBack | Headings/labels are announced, focus order is logical, errors use alerts, live status changes are announced, and controls have discernible names | |
| Contrast/reduced motion | Required | Recommended | Required | Recommended | Text and focus indicators remain visible in light/dark preference; no workflow depends on animation | |

## Completion gate

Phase acceptance requires all **Required** cells to pass. File defects with the
row, device/OS/browser version, orientation, network condition, join path, and
reproduction steps. Do not attach screenshots containing response content,
join URLs still in use, or persistent browser storage identifiers.

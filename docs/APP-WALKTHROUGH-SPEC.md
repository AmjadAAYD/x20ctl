# X20CTL: the app, step by step

An ordered walkthrough of what the app does from the moment it opens, one moment at a time:
what you see, what you do, what happens, what it looks like, how it moves.

How to read it:

- **Real** marks names, labels and behaviour taken from the current source (tab names, button text, card titles, model list). Do not rename them.
- **Target** marks the motion and layout wanted for the redesign. These are the spec, not a description of the current build.
- Every screen must show real data only: values from the connected pad or the saved profile, or an honest empty state. No invented numbers, no filler jargon.
- Every theme (Default, Nexus, Dusk, Hollow, Nocturne, Sunrise) follows this same order and the same names. A theme changes the look and the intro, never the flow, the labels or what a control does.

Motion rules for everything below (Target):

| Rule | Value |
|---|---|
| Hover reaction | 120 ms, small lift or glow |
| Press reaction | 80 ms down, 140 ms settle |
| Tab/page change | 220-280 ms, outgoing fades and slides 12 px, incoming arrives from the same direction |
| Panel open/close | 200 ms ease-out, 160 ms ease-in |
| Success confirmation | one 400 ms pulse on the thing that changed, then rest |
| Idle | nothing loops unless it shows live state (stick position, battery, link). Idle CPU stays near zero |
| Reduced motion | all travel becomes a 100 ms crossfade |
| Sound | one shared quiet family for every theme: Tab, Pick, Hover, Apply, Error. Nothing downloaded; sounds are synthesised |

---

## 0. Before the app: install and first launch

1. **Setup is the same exe.** Running it offers Install; the installed copy has an uninstaller and auto-update on by default. (Real)
2. On first run with no ViGEmBus present, the app **asks first** before installing the driver, with a setup toggle. It never installs silently. (Real)
3. Language: the app starts in the saved language. Changing it takes effect the next time X20CTL opens, and says so. (Real)

---

## 1. Opening the app

### 1.1 The intro

1. The window opens. Before anything else is drawn, the **intro film** for the chosen theme plays full window. (Real: Intros are per theme.)
2. The intro is on by default and **skippable at any moment** (click, Esc, any pad button). Skipping never leaves a half-drawn frame: it cuts to the Controller Zone at its resting state.
3. The intro is silent. No theme has its own sound.
4. The X20CTL mark flies from the intro into the **header logo** position, so the logo you see in the header is the same object that ended the film. (Real direction, owner-approved.)
5. Each theme's intro tells **its own one-idea story**; none shares a fly-in template.
6. Target length is short, under three seconds of story, then the hand-off into the Zone. No long cutscene.
7. If the intro is turned off (Tools > Themes), the app opens straight to the Zone with only the logo settle.

### 1.2 First paint after the intro

1. Header bar fades in first (logo, then right-side actions).
2. The four player docks rise in one after another, left to right, 60 ms apart.
3. The footer hint appears last. Nothing flickers or rearranges after this point.

---

## 2. The Controller Zone (home)

**Real header, left to right:**

- X20CTL logo and the word **X20CTL**
- The label **Controller Zone**
- Right side: **Tools** button, **Support** button, the live **clock**, then minimise, maximise, close.

**Real page heading:** **Player slots**, subtitle **Choose a player to set up its controller**.

### 2.1 The four docks

1. There are **four docks**, one per player (Player 1 to Player 4). All four begin equal. (Real: "All four docks begin equal.")
2. Each dock shows the controller silhouette for its model, the player name, and the connection state.
3. An empty dock shows an honest empty state with **+ Add controller**. A filled dock shows **Switch Controller** and **Remove**. (Real)
4. Hover on a dock: it lifts, its outline lights along the controller's own edge (the outline follows the pad's real shape, never a hand drawing). Hover sound plays.
5. Moving the mouse across the four docks makes the hover travel; only the dock under the pointer is lit.
6. Footer hint text: **Select any player. All four docks begin equal.**

### 2.2 Adding a controller

1. Click **+ Add controller**.
2. The **model selector** opens over the Zone (panel opens in 200 ms, background dims).
3. The selector lists the models the app knows (Real): **X20**, **X20 Pro**, **X05 Pro**, **X10**, **D10**, **Dune**, **X15**. Each card shows the pad art, its name, and what the profile knows (stick sensor, trigger, back buttons M1-M2 or M1-M4/M6, vibration motors, polling, gyro, battery levels, extras).
4. Hover on a model card shows a slow turn of the pad art and its specs. Click picks it: **Pick** sound, the card locks, the panel closes, and the dock fills with the pad.
5. Colour finishes cycle for each pad (for example X20 Pro White, Dune Black, D10 Farout Purple, X15 Wukong by default). The cycle blends rather than cutting.
6. If the pad is physically connected, the dock goes live; if not, it says **Controller offline** and still lets you edit a profile.

### 2.3 Selecting a player

1. Click a dock (or press A on a pad already navigating the Zone).
2. The chosen dock **grows to the hero position**: hero left, other three stack on the right as small docks. The swap is animated (about 280 ms), not a cut.
3. The heading changes to the selected player; a button **Return to four-player overview** appears (Real) and reverses the animation.
4. From here **Open studio** (or double-click) goes into the Studio for this controller (section 3).

### 2.4 Switching and removing

- **Switch Controller** reopens the model selector for that dock; the previous pad's art morphs to the new one.
- **Remove** asks once, then the dock empties with a fade. Nothing else moves.

### 2.5 Header actions

- **Tools**: opens the Tools hub (section 4).
- **Support**: opens the Ko-fi link.
- **Clock**: ticks quietly; no animation beyond the digit change.
- Window controls follow normal Windows behaviour. Minimise to the tray keeps idle CPU near zero.

### 2.6 Update notice

If a newer GitHub release exists, a quiet **New update available** appears in the top bar. There is no popup at launch. Clicking it offers the download and shows progress ("Downloading ...").

---

## 3. The Studio (one controller)

Entered from a dock. The pad is the centre of every page.

### 3.1 Shell

**Real, top row:** X20CTL logo, a title area with the page name, and a live connection readout (gameplay link). A **Working Draft** label means edits are staged, not sent.

**Real navigation bar** under it, with previous and next bumpers (LB/RB on a pad). Tab names, in this exact order:

1. **Dashboard**
2. **Buttons**
3. **Curves**
4. **Macros**
5. **Vibration**
6. **PC mode**
7. **RGB** (X20 Pro only)
8. **Screen** (only models with a screen, such as the Dune LCD)
9. **Device**
10. **Mouse**
11. **Setups**

**Management pages** reached from the same bar or from Tools, hidden when not relevant: **Diagnostics**, **Tester**.

Tab behaviour:

1. Clicking a tab slides a short **indicator** to it. It travels rather than jumping and stretches slightly on the way (Target ~240 ms).
2. If the window is too narrow, tabs that do not fit collapse into a **More** menu; the active tab stays reachable and keeps the indicator on More.
3. Previous/next bumpers wrap around.
4. A tab sound (**Tab** cue) plays on change.

**Real footer on every page:**

- **ChangeCount**: "No unsent changes", or "N unsent changes".
- **Apply Changes** button: disabled until something is staged.
- **StatusNote**: "Controller offline" when no pad is connected.

Apply sends changes to the controller (X20 Pro writes on-pad; PC-mode effects apply to the virtual pad only; the page says which). Undo stays available after Apply.

### 3.2 Dashboard

What it is: the pad's current state at a glance.

1. The pad art in its real colour, large, centre.
2. Live readouts that come from the pad: connection, battery, firmware where known, active profile, number of staged changes.
3. On opening, readouts count in from the previous value; they do not reset to zero.
4. If a value is unknown it shows **Not published** / **Unknown** (as in the model profile), never a made-up number.
5. Sticks and triggers move live on the art as you press them.

### 3.3 Buttons

Real labels: canvas hint **FRONT · PHYSICAL CONTROLS**; panel **PHYSICAL INPUT**, **Choose a control**; mapping row **CURRENT** then an arrow then **OUTPUT**; **CHOOSE OUTPUT**; **SPECIAL**; **PADDLE NAVIGATION**; **All assignments**.

Step by step:

1. The pad is drawn front-on. Press a physical button, or click one on the art, to select it. The selected control rings and the right panel names it.
2. The mapping tile shows **Current** on the left, an arrow, and **Output** on the right.
3. The arrow carries a small push when the output changes.
4. Choosing an output from **CHOOSE OUTPUT** updates the Output tile with a short swap animation and raises the unsent-change counter.
5. **SPECIAL** holds non-button outputs (for example turbo, which is a script, not hardware; M paddles are A/B, never real separate buttons).
6. A rear view shows M-paddles; **PADDLE NAVIGATION** lets you move between them with the pad.
7. **All assignments** switches to a table of every control and its output, with unsent rows marked.
8. Nothing is written until **Apply Changes**.

### 3.4 Curves

1. Choose a stick or trigger, then a curve preset (the preset list is real: the app's built-in curve presets).
2. A graph shows the curve; a live dot follows the actual stick on the graph as you move it.
3. Dragging a handle reshapes the curve with the dot still live. Deadzone and range are explained in one line each next to their control.
4. Changing a curve marks it unsent; Apply writes it.

### 3.5 Macros

1. A list of macros, a **Record** control, and a timeline.
2. **Record, don't design.** Pressing Record captures real presses and real timings; guessed timings are not offered as the default.
3. The timeline fills left to right as you press; the recorded length shows live.
4. Playback highlights each step as it runs.
5. Limits are stated up front (entry cap, minimum dodge window), as numbers from the profile, not from imagination.
6. Save, rename, delete; Apply writes to the pad where the model supports it.

### 3.6 Vibration

1. The pad art shows the motor regions the model really has (grips; X20 Pro grips and triggers per profile). Outlines are the pad's own edges.
2. Each region has a strength control; moving it plays a short test pulse on the pad and a matching ripple on the art.
3. A long sweep animation runs along the outline when a region is active.

### 3.7 PC mode

1. Explains in two lines that this page affects the **virtual Xbox pad on this PC**, not the pad's own memory.
2. Remaps, turbo and macros for any pad. Status shows whether the virtual pad driver and hiding of the physical pad are active.
3. Changes show as PC-mode, kept visibly separate from on-controller settings.

### 3.8 RGB (X20 Pro only)

1. Colour and brightness for the stick rings. Brightness writes to the pad (range 0x10-0xFF).
2. A live preview ring shows the colour as you pick it.
3. A battery-level colour mode (app-driven) is available; the page says it runs from the app.

### 3.9 Screen (models with a screen)

1. Upload an image or media to the pad's screen. Shows the supported size and progress.
2. Progress is a determinate bar with a real byte count; a failed upload says so and offers retry.

### 3.10 Device

1. Model, connection, firmware where known, pad settings: shutdown timer, gears, presets, calibration, factory reset.
2. **Factory reset** has a confirm step before anything is sent.
3. Anything the pad does not expose is shown as unavailable, with the reason.

### 3.11 Mouse

1. Turns the pad into a mouse: sticks move the cursor, buttons click.
2. Sensitivity and button choices sit beside a live preview.
3. While Mouse mode is on, menu navigation is paused so nothing double-presses.

### 3.12 Setups

1. Named bundles of everything above (Buttons, Curves, Macros and so on).
2. **Save setup**, **Load**, **Rename**, **Delete**, with a one-step **Undo** history.
3. Loading a setup stages it as unsent; it does not write until Apply.

### 3.13 Diagnostics

1. Live raw input rate, neutral drift per stick (the unit is neutral drift), battery.
2. A **Capture** records a drift sample; shows min, max, mean in plain numbers.

### 3.14 Tester

1. The pad art with every button and axis lit as it is pressed.
2. Button log scrolls with the last presses and their timestamps.

---

## 4. Tools hub (from the Zone's Tools button)

Opens as a panel over the Zone (200 ms). **Real cards**, in this order of purpose:

1. **Controller not working?**: the hero card. "Read scan, then Write scan, then send both reports online together." Three chips: **1 Read scan**, **2 Write scan**, **3 Send online**.
2. **PC mode**: "Remaps, turbo and macros for any pad."
3. **Write scan**: "What an app writes. 20 to 40 min."
4. **Send feedback**: "Report a bug, ask for a feature or tell us about your controller."
5. **Themes**: "Layouts, controls and the opening intro."
6. **Replay Intro**: "Watch the X20CTL opening again."
7. **About**: "What X20CTL is, who made it, and credits."
8. **Support X20CTL**: "Help keep it going on Ko-fi."
9. **Star on GitHub**: "AmjadAAYD/x20ctl".

Each card has its own colour, an icon, a hover lift, and a **Pick** cue on click. Opening a card slides it in from the right; **Back** returns.

### 4.1 Read scan and Write scan

1. Every step says **exactly what to do**, with a **countdown timer**. The Read scan never talks to the pad in a writing way.
2. Verdict and the Filtered-log check show at the end.
3. Nothing is uploaded until you review it. Export is local by default; upload is a separate, explicit step.

### 4.2 Themes

1. Shows the six themes as cards, each in its own face: **Default**, **Nexus**, **Dusk**, **Hollow**, **Nocturne**, **Sunrise**.
2. Choosing one says "starts the next time X20CTL opens" unless it can apply live.
3. Intro on/off toggle sits here.

### 4.3 Replay Intro

Plays the current theme's intro again, skippable, and returns to Tools.

### 4.4 Language and updates

Language list with a note when a change needs a restart. Update card shows "<release> is out. You have <version>." with a download button and progress.

---

## 5. Agent XCTL

1. A round button in the app opens the Agent window.
2. The agent works **offline only** on a small local model. It explains each setting in plain words, with a slow walk-through.
3. It first shows **"I read that as ..."** so you can correct it before anything is staged.
4. Strict validation runs **before** anything is applied. Results describe exactly what the app changed.
5. **Undo** is always available. The agent never writes to the pad on its own.
6. LB = start over.

---

## 6. Controller navigation (no mouse)

1. Everything above is reachable with a pad: stick or D-pad to move, A to select, B to go back, LB/RB for previous/next tab.
2. Navigation is diagnostic-first: it reports what was pressed. Repeated navigation announcements are suppressed.
3. In Mouse mode or PC mode with the virtual pad active, menu navigation pauses so game input is not double-handled.

---

## 7. Closing and the tray

1. Close hides to the tray when set; the tray keeps idle CPU near zero (no permanent render handlers).
2. Exit from the tray shuts the app down cleanly, restoring any hidden physical pad.

---

## 8. What is deliberately not in the app

- No theme-specific sounds, no downloaded sounds.
- No per-player colours; one accent on a dark base.
- No claim of support beyond what a verified profile shows. Unknown features say **Unknown** or **Not published**.
- No removed themes (Pulse, Yozakura, Rebel, Spotlight, Tide, Volt). Their names must not appear anywhere.

---

## 9. Open decisions for the owner

These are not specified yet because they are creative calls that need agreeing first:

1. The exact motion and composition of each theme's Zone and Studio (this document fixes names, order and behaviour, not the art).
2. Whether the Dashboard gets the Buttons-page background.
3. Where "Agent XCTL" sits in the header.
4. Any new theme or the redesign of an existing one: concepts and rendered previews first, code second.

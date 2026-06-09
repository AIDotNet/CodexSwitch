# UI Web Parity Agent Plan

## Goal

Make CodexSwitch desktop components behave and feel like the Web surface as closely as Avalonia allows. The source of truth is:

- Web visual baseline: `docs-site/app/global.css` and Web pages under `docs-site/app`.
- Desktop component target: `CodexSwitchUI/src/CodexSwitchUI`.
- Legacy migration source: `CodexSwitch/Controls`, `CodexSwitch/Styles/Components`, and XAML views using `xmlns:ui="using:CodexSwitch.Controls"`.
- Architecture reference: `irihitech/Semi.Avalonia`, especially its `SemiTheme.axaml` entry, `Controls/_index.axaml`, `Themes/*/_index.axaml`, `Tokens/_index.axaml`, and native-control theme split.

## Architecture Direction

CodexSwitchUI should move toward a Semi.Avalonia-like package shape:

1. One theme entry point that merges token resources, theme variant dictionaries, component styles, primitives, and localization-aware defaults.
2. Component styles owned by the library, with templates that do not fall through to Fluent default chrome.
3. Dynamic resources for every color, spacing, radius, typography, opacity, and motion value.
4. Component classes that expose Web-equivalent state through stable properties and classes, not page-local visual hacks.
5. Legacy `Cs*` controls kept only as migration shims until all app views use `CodexSwitchUI.Controls`.

Semi.Avalonia evidence refreshed on 2026-05-26:

- `SemiTheme.axaml` is the application-facing entry point and merges theme dictionaries, controls, shared themes, tokens, locale, icons, and styles.
- `Controls/_index.axaml` is a native-control style index that includes component files such as `Button.axaml`, `DropDownButton.axaml`, `SplitButton.axaml`, `ScrollViewer.axaml`, `Tooltip.axaml`, and `PipsPager.axaml`.
- `Themes/{Light,Dark,HighContrast}/_index.axaml`, `Themes/Shared/_index.axaml`, `Tokens/_index.axaml`, and package-specific theme entries show the direction CodexSwitchUI should keep moving toward: one public theme entry plus separated component, token, primitive, locale, and optional package layers.

## Shared Interaction Contract

Every interactive component must align with the Web state model:

- `pointerover`: visual hover only. It must not mutate data or selection.
- `pressed`: transient active feedback, released by the native input lifecycle.
- `focus-visible`: keyboard/programmatic focus ring. Pointer focus must not show the ring.
- `disabled`: no hit testing, reduced opacity through `CodexSwitch.DisabledOpacity`.
- `loading`: command/click suppression when the component represents an in-flight action.
- `selected` or `active`: explicit state driven by a property or command result.
- `open` or `closed`: explicit overlay/disclosure state, with enter and exit motion.

## Agent Task Template

Each agent task must include:

1. Scope: component names, files, and old `Cs*` equivalents.
2. Web evidence: exact Web component or CSS state to match.
3. Avalonia evidence: current control class, AXAML template, tests, and view usage.
4. Implementation plan: token changes, class properties, template parts, pseudo-classes, keyboard/pointer behavior, and page migration.
5. Verification: unit tests, XAML compile, targeted app smoke route/window, screenshot or visual gallery check where possible.
6. Non-goals: unrelated business logic, unrelated page redesign, and unrelated localization churn.

## Agent Work Packages

### Agent A: Token And Motion Foundation

Scope:

- `docs-site/app/global.css`
- `CodexSwitchUI/src/CodexSwitchUI/Tokens`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/CodexSwitchThemeManager.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/*.axaml`

Tasks:

- Extract Web colors, radius, typography, and motion into a single manifest.
- Generate or manually mirror CSS variables and Avalonia resource keys.
- Replace hard-coded transition durations in component styles with `CodexSwitch.MotionDuration*`.
- Add tests that forbid new hard-coded component motion values except documented low-level animation loops.

Acceptance:

- Button and text input motion use token resources.
- Reduced motion still resolves to zero duration through `CodexSwitchThemeManager`.
- Tests prove token resources exist and representative components consume them.

### Agent B: Interaction State Foundation

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls`
- Button, input, select, checkbox, radio, switch, slider, tabs, command, menu, context menu, popover, dialog, toast, collapsible.

Tasks:

- Add a shared focus-visible rule for every keyboard-focusable control.
- Ensure pointer focus suppresses focus ring.
- Ensure loading state suppresses command activation.
- Ensure `open`, `selected`, and `active` states are property-driven and testable.
- Map Escape, Enter, Space, arrow keys, Tab, and outside-click behavior for overlay/navigation controls.

Acceptance:

- All interactive controls expose the same Web state names in classes or pseudo-classes.
- Tests cover click suppression, focus-visible, keyboard navigation, and open/close events.

### Agent C: Missing Core Components

Scope:

- New controls in `CodexSwitchUI/src/CodexSwitchUI/Controls`
- New styles in `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls`
- Component structure tests.

Initial components:

- `CodexTextarea`: Web textarea equivalent, multiline input with wrapping, placeholder, intent, disabled, read-only, size, and focus-visible states.
- `CodexKbd`: keyboard shortcut token with size classes.
- Later: tooltip, hover card, dropdown button, split button, native scroll area wrapper, empty state, pagination, command dialog.

Acceptance:

- Each component has a class file, style file, theme include, template parts, motion, and tests.
- No component depends on Fluent default templates for its visible surface.

### Agent D: Legacy Component Migration

Scope:

- Views still importing `CodexSwitch.Controls`.
- Old styles under `CodexSwitch/Styles/Components`.

Tasks:

- Migrate one page or dialog at a time from `Cs*` to `CodexSwitchUI.Controls`.
- Preserve bindings, command names, i18n keys, and layout intent.
- Delete or deprecate old styles only when no view references them.

Acceptance:

- `rg 'using:CodexSwitch.Controls|<ui:Cs' CodexSwitch/Views` trends down after every pass.
- No unrelated business files are touched.

### Agent E: Visual Verification

Scope:

- `CodexSwitchUI.Docs` component gallery.
- Desktop app windows using updated controls.
- Web reference pages.

Tasks:

- Add gallery states for default, hover, pressed, focus-visible, disabled, loading, selected, open, and error.
- Capture Web and Avalonia reference screenshots for each component family.
- Record known platform limitations where Avalonia cannot exactly match browser behavior.

Acceptance:

- Visual checks exist before claiming a component family is Web parity complete.
- Every intentional mismatch has a documented reason.

## Current First Slice

This first implementation slice starts Agent B and Agent C:

- Add focus-visible behavior to `CodexButton` and `CodexTextBox`.
- Move Button/Input motion toward token-backed Web timing.
- Add `CodexTextarea`.
- Add `CodexKbd`.
- Extend component tests so these additions are part of the library contract.

## Current Progress

Completed slices:

- Button and text input now use `CodexFocusVisible` so pointer focus suppresses the focus ring and keyboard/programmatic focus shows `:focus-visible`.
- `CodexTextarea` and `CodexKbd` were added as first missing core components with owned style files and theme includes.
- Select, checkbox, radio, switch, and slider now use the same focus-visible event contract as button/input.
- Button, input, textarea, select, checkbox, radio, switch, slider, and kbd now consume `CodexSwitch.MotionDuration*` and `CodexSwitch.MotionEaseOut` instead of hard-coded transition durations.
- Component tests now guard form motion token usage and the focus-visible source/style contract.
- Tabs, command, menu, context menu, collapsible, and navigation menu now use focus-visible selectors for the interaction chrome covered in this slice.
- Tabs, menu, and context menu now create Codex-owned item containers by default, so focus-visible behavior is controlled in component code instead of falling through to native item focus.
- Tokenizable transitions in tabs, command, menu, context menu, collapsible, and navigation menu now consume `CodexSwitch.MotionDuration*` and `CodexSwitch.MotionEaseOut`.
- Tokenizable transitions across all component style files now consume `CodexSwitch.MotionDuration*` and `CodexSwitch.MotionEaseOut`.
- Component structure tests now reject hard-coded component motion durations except documented Avalonia page-transition primitives.
- Known Avalonia limitation: `CrossFade.Duration` and `PageSlide.Duration` are plain `TimeSpan` properties, not styled properties, so they cannot consume `DynamicResource` motion tokens directly. Keep these literals documented until the library owns a token-aware page transition wrapper.
- Agent B1 first pass is implemented for keyboard-triggered navigation/disclosure behavior: tabs now handle roving arrow/Home/End selection and Enter/Space activation, navigation menu items handle Enter/Space activation plus orientation-aware arrow/Home/End movement, navigation menus close on Escape, and collapsible triggers share a tested Enter/Space toggle path.
- Web event evidence for this B1 pass: local `docs-site/content/docs/*/ui-system/navigation.mdx` requires active state and keyboard movement to be visible; Radix Web primitives document the same keyboard model for navigation menus, tabs, and collapsibles.
- `CodexTooltip` was added as the next missing overlay primitive with class, style file, theme include, open/closed state classes, side placement classes, optional arrow, tokenized motion, and tests.
- Agent B2 first pass now covers overlay dismissal beyond the high-level controls: `CodexOverlay` has Escape and outside-pointer dismissal hooks, `DismissCommand`, `CloseOnEscape`, and `DismissOnOutsidePointer`; command items inside a loading `CodexCommand` suppress activation and command execution.
- Agent B2 second pass now adds Web-style focus-return requests for dismissible surfaces: `CodexDialog`, `CodexCommandDialog`, `CodexPopover`, `CodexDropdownButton`, and `CodexSplitButton` expose `RestoreFocusElement`, `RestoreFocusOnDismiss`, `RestoreFocusRequested`, and tested loading/disabled-safe close paths so closing a layer can hand focus back to its trigger.
- Agent B2 rendered pass now adds an Avalonia headless test harness for mounted overlay behavior. It proves a dialog dismissed by Escape moves actual focus from an inner field back to its trigger, and proves `CodexOverlay` pointer routing keeps inside-content clicks open while outside/scrim clicks dismiss.
- Agent B2 popup-native pass now adds a shared `CodexMenuActivation` gate for `CodexMenuItem` and `CodexContextMenuItem`. Disabled items, command `CanExecute=false`, and loading parent menus suppress activation through click, keyboard activation, and pointer release paths. `CodexDropdownButton` and `CodexSplitButton` also restore focus when a native Popup light-dismiss or direct two-way `IsOpen=false` closes the popup.
- Agent C2 selected `CodexCommandDialog` as the next high-value missing core component. It composes `CodexDialog` dismissal with `CodexCommand` search rows, close-on-select behavior, loading suppression, owned style file, theme include, tokenized enter/exit motion, docs gallery coverage, and regression tests.
- Primitive motion is now also tokenized for overlay, focus ring, and typography styles, with tests rejecting new hard-coded primitive transition durations.
- Agent C3 selected `CodexHoverCard` as the next missing Web overlay primitive. It follows Radix Hover Card evidence: default closed state, controlled `IsOpen`, `OpenDelay` 700ms, `CloseDelay` 300ms, pointer/focus open and delayed close requests, Escape dismiss, side/align/open/closed classes, optional arrow, tokenized motion, docs gallery coverage, and tests.
- Agent C4 selected `CodexDropdownButton` as the next missing Web menu trigger primitive. It follows Radix Dropdown Menu evidence: trigger-owned open/closed state, light-dismiss popup content, Escape dismiss, side/align classes, optional arrow, close-on-select behavior, disabled/loading suppression, Codex-owned trigger/surface templates, docs gallery coverage, and tests. Semi.Avalonia evidence also treats `DropDownButton.axaml` as an indexed control style next to `SplitButton.axaml`.
- Agent C5 selected `CodexSplitButton` as the next missing Web action/menu primitive. It follows the Web split-button contract: primary click executes only the main action, secondary click opens the menu, loading/disabled/CanExecute suppress the right action surface, dropdown content keeps the same Escape, light-dismiss, side/align, optional arrow, and close-on-select behavior as `CodexDropdownButton`. Semi.Avalonia evidence uses separate `PART_PrimaryButton` and `PART_SecondaryButton`; CodexSwitchUI mirrors that shape with Codex-owned button parts and joined corner radii.
- Agent C6 selected `CodexPagination` as the next missing Web data-navigation primitive. It follows Web pagination behavior with 1-based pages, boundary and sibling ellipses, current-page state, first/previous/next/last actions, disabled and loading suppression, Home/End/arrow-key navigation, small-page full expansion, Codex-owned page buttons, docs gallery coverage, docs-site coverage, and structure/behavior tests. Semi.Avalonia evidence from `PipsPager.axaml` supports keeping pager buttons and indicator/page items under one indexed control style.
- Agent C7 selected `CodexScrollArea` as the next missing Web viewport primitive. It follows Radix-style scroll area expectations with a scoped root/viewport/scrollbar/thumb/corner surface, hover and active-scroll scrollbar visibility classes, inset-content mode, offset/extent/viewport metrics, boundary classes, scroll changed event forwarding, docs gallery coverage, docs-site coverage, and structure/behavior tests. Semi.Avalonia evidence from `ScrollViewer.axaml` supports owning the ScrollViewer and ScrollBar templates together while keeping this pass scoped to the new wrapper instead of replacing global app scroll viewers.
- Agent C8 selected `CodexEmptyState` as the next missing feedback primitive. It gives empty/no-result views an owned icon/header/content/action template, semantic variants, size classes, loading/disabled/CanExecute trigger suppression, primary and secondary action events, docs gallery coverage, docs-site coverage, and structure/behavior tests. Semi.Avalonia has no dedicated Empty component, so this pass stays Codex-owned while following the same indexed style and placeholder-token direction.
- Agent C9 selected `CodexField` as the next form primitive needed before migrating legacy `CsField` usages. It is now a first-class control/style pair with label, description, message, required, intent, and size classes, docs gallery coverage, docs-site forms coverage, and structure/state tests. The previous shell-local `CodexField` definition/style has been extracted so forms no longer depend on `ApplicationShell.axaml` for field layout.
- Agent C10 first migration pass moved the `AddProviderPage` field wrappers from legacy `CsField` to `CodexField`, keeping the existing `CsInput`/`CsSelect` controls, bindings, layout rows, and business behavior untouched. This proves the new form field primitive can land in a real app page before wider legacy form migration.
- Agent C11 migrated the `SettingsPage` settings form field wrappers from legacy `CsField` to `CodexField` across route, auth, resilience, and usage pricing sections. The pass kept old `CsInput`/`CsSelect`/`CsSegmentedControl` children, tab visibility, commands, bindings, and layout untouched.
- Agent C12 migrated the `ProviderEditorDialog` field wrappers from legacy `CsField` to `CodexField` across connection, protocol routing, usage query, model route, and model conversion sections. This pass covered nested `ItemsControl` data templates while preserving row bindings, delete commands, switches, textarea/input/select children, and dialog behavior.
- Agent C13 migrated the final `CsField` wrappers in `ModelEditorDialog` and `ClaudePage` to `CodexField`. `CodexSwitch/Views` now has no remaining `<ui:CsField>` usages, so the legacy field container has been removed from active view markup while old input/select/button primitives remain for later migration.
- Agent C14 migrated the `ModelEditorDialog` text inputs from legacy `CsInput` to `CodexTextBox`, keeping the previously migrated `CodexField` wrappers and preserving all pricing/model bindings and placeholders. This starts moving real input behavior, focus-visible styling, and tokenized input motion from CodexSwitchUI into app forms.
- Agent C15 migrated the `AddProviderPage` provider creation form controls from legacy `CsInput`/`CsSelect` to `CodexTextBox`/`CodexSelect` inside `CodexField`. The pass preserved provider bindings, password masking, placeholders, protocol selection, and layout, while leaving OAuth account row editing for a later data-row migration.
- Agent C16 migrated the `ProviderEditorDialog` connection and protocol routing fields from legacy `CsInput`/`CsSelect` to `CodexTextBox`/`CodexSelect`. This brings the provider editor's core name/model/note/website/base URL/API key/protocol/service-tier form behavior onto CodexSwitchUI while leaving usage-query mappings and dynamic route rows for later passes.
- Agent C17 migrated the `ProviderEditorDialog` usage-query request and response mapping cluster from legacy `CsInput`/`CsSelect`/`CsTextarea` to `CodexTextBox`/`CodexSelect`/`CodexTextarea`. This covers method, timeout, URL, headers, body, and JSON path fields while intentionally leaving dynamic model route/conversion rows for a later row-template pass.
- Agent C18 migrated the deferred `ProviderEditorDialog` dynamic model route and model conversion row controls from legacy `CsInput`/`CsSelect` to `CodexTextBox`/`CodexSelect`. This preserves add/remove row commands, read-only default conversion source rows, editable target gating, placeholders, and protocol selection while completing the provider editor form-input migration.
- Agent C19 migrated the `SettingsPage` language, route/network, auth, resilience, and usage-pricing form inputs from legacy `CsInput`/`CsSelect` to `CodexTextBox`/`CodexSelect`. This keeps the settings tab layout, item templates, bindings, switches, segmented controls, and save/apply commands untouched while moving the page's text/select behavior onto CodexSwitchUI.
- Agent C20 migrated the final active-view legacy form-input surfaces: `CodexAuthImportDialog` JSON textarea, `ClaudePage` model field, and `AddProviderPage` OAuth account display-name row. `rg '<ui:CsInput|<ui:CsSelect|<ui:CsTextarea' CodexSwitch/Views` now returns no matches, and `AppViewMigrationTests` guards that invariant across every active view.
- Docs Agent A1 started the full `CodexSwitchUI.Docs` rewrite with a categorized three-column shell, independent `DocsPage` records for menu pages, a right-side AXAML source rail, standalone copied AXAML example files, and a Monaco-like `DocsCodeBlock` with line numbers and clipboard copy support. Placeholder pages remain for component families whose examples still need dedicated cases.
- Docs Agent A2 first pass expanded every currently registered Docs menu item into a unique sample path and real preview builder. It added dedicated AXAML examples for Field, DropdownButton, Dialog, Popover, Table, Pagination, and Motion tokens; added per-page behavior/event notes; and added tests that reject reused sample paths and placeholder preview registrations.
- Dark Agent D1 fixed the Docs dark-theme regression by replacing hard-coded light shell surfaces with CodexSwitch theme resources, refreshing themed borders after runtime theme changes, and avoiding full tree rebuilds during theme toggles.
- Runtime stability fix: Docs page navigation now caches page and right-rail controls and toggles `IsVisible` instead of replacing `_pageHost.Content`. A rendered headless test navigates Dropdown, Dialog, Table, Pagination, switches Dark, and navigates Motion to cover the crash path reported from Avalonia transition detach. Pagination template also no longer uses the invalid `$parent[controls:CodexPagination]` binding that crashed at render time.
- Docs Agent A3 first pass added structured state and event matrices to every current Docs page through `DocsStateCase` and `DocsEventCase`. Each page now renders a state matrix, an event matrix, and a right-rail event summary covering Web-style states such as loading, focus-visible, selected, open/closed, restore-focus, ellipsis, and reduced motion.
- Crash Agent D2 tightened the Docs navigation crash fix by making the topbar a stable control tree. Navigation and theme changes now call `RefreshTopbar(...)` to update text and theme-button variants instead of replacing `_topbar.Child`, so animated controls are not detached during menu clicks.
- Motion Agent M1 tokenized runtime table refresh motion. `CodexTable` and `CodexPinnedTable` now resolve `CodexSwitch.MotionDurationDefault` and `CodexSwitch.MotionEaseOut` from theme resources for content/page refresh transitions, and skip the opacity/translate dip when reduced motion resolves duration to zero.
- Motion Agent M2 first pass added shared runtime motion resolution through `CodexMotion`, moved table runtime transitions onto that helper, made `CodexCollapsible` height animation duration style-bound to `CodexSwitch.MotionDurationSlow`, and made `CodexSonnerService` enter/exit timing resolve from `CodexSwitch.MotionDurationDefault` with immediate open/remove behavior when reduced motion resolves to zero.
- Motion Agent M2 second pass tokenized the remaining targeted runtime reveal/loading surfaces: `CodexUsagePieChart` now exposes style-bound `AnimationDuration` backed by `CodexSwitch.MotionDurationSlow`, `CodexSkeleton` now derives its default pulse duration from `CodexSwitchThemeOptions.SkeletonShimmerDuration`, and structure tests now maintain an explicit allowlist for true frame timers and Web behavior delays.
- Motion Agent M3 first pass added headless rendered lifecycle verification for reduced-motion final states and normal-motion tokenized intermediate contracts across table, collapsible, sonner, skeleton, and usage pie chart. The pass also made static chart fallback brushes immutable so repeated headless render sessions do not fail on Avalonia thread ownership.
- Motion Agent M3 second pass extended rendered timing verification to HoverCard and ScrollArea: mounted hover cards now prove Radix-style 700/300ms delays do not open/close immediately while zero delays do, and mounted scroll areas now prove hover/scroll visibility states are class-driven with nonzero opacity transitions on real template scrollbars.
- Docs Agent A5 expanded the desktop Docs gallery from the earlier 13 registered pages to 41 standalone pages/samples. The new coverage includes split button, textarea, checkbox, radio, switch, slider, alert, badge, avatar, toast, sonner, spinner, progress, skeleton, navigation menu, menu, context menu, command, collapsible, separator, kbd, command dialog, tooltip, hover card, card, scroll area, ranked bar chart, and usage pie chart.
- Docs Agent A5 also changed the preview contract so every page renders the component preview followed by a `Show code` button and an inline `DocsCodeBlock` for that exact AXAML sample. The right rail source viewer remains available, but the current example code now expands directly under the component as requested.
- Docs Agent A5 verification added tests for inline expandable code, broad top-level component sample coverage, standalone copied AXAML files for every registered page, and representative rendered Docs pages across light/dark/custom themes. `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0` passed with 137 tests.
- Docs Agent A6 first pass turned Docs examples from one preview per page into a multi-case model through `DocsExampleCase`. Every page still has its default inline-code example, and representative high-risk component pages now add dedicated state samples for button variants/loading/disabled, textbox validation, switch checked/disabled states, toast open/closed/semantic states, skeleton animated/static shapes, collapsible open/closed states, dialog open/closed states, hover-card open/closed delay states, pagination boundaries/loading/compact states, and table density/selected/disabled states.
- Docs Agent A6 verification now guards that multi-case pages keep their own standalone AXAML samples and that every rendered case still exposes the local `Show code`/`Hide code` inline `DocsCodeBlock` contract. `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0` passed with 138 tests.
- Docs Agent A6 second pass added headless screenshot verification for expanded inline-code examples across light, dark, and custom themes. The test navigates the high-risk multi-case pages, expands every `Show code` toggle, asserts visible inline `DocsCodeBlock` instances map to standalone AXAML samples without missing-loader fallbacks, keeps the right-rail source visible, and captures rendered frames after expansion. This pass also hardened `DocsCodeSamples` so tests and non-Docs hosts can find samples from the repository `src/CodexSwitchUI.Docs` fallback instead of returning placeholder source. `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0` passed with 139 tests.
- Docs Agent A7 first pass added a lightweight comparable visual baseline for the expanded Docs gallery. `DocsVisualFingerprints.json` stores reduced-motion visual fingerprints for representative multi-case pages across light, dark, and custom themes; the rendered test now recomputes those fingerprints after expanding inline code and compares them with a small quantized-color tolerance. The baseline can be refreshed explicitly with `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1`, while normal test runs compare against the committed manifest. `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0` passed with 140 tests.
- Docs Agent A7 second pass expanded the comparable visual baseline from four representative pages to all ten high-risk multi-case Docs pages: button, textbox, switch, toast, skeleton, collapsible, dialog, hover card, table, and pagination. The manifest now contains 30 light/dark/custom fingerprints generated after inline code expansion, and the rendered test uses the same `MultiCasePages` list as the expanded-code smoke test so the two gates stay aligned.
- Agent B2 submenu-keyboard pass added explicit Web-style menu key handling for `CodexMenuItem` and `CodexContextMenuItem`: Enter/Space open submenu triggers, Right opens nested submenus and transfers focus to the first child, Down opens Codex menu submenu triggers, Left/Escape close an open submenu and return focus to the parent, and Up/Down/Home/End move sibling focus while skipping disabled/loading/`CanExecute=false` items. `NavigationDataComponentTests` covers menu/context-menu open/close paths plus deterministic context submenu `submenu-open` class sync, and `MenuRenderedLifecycleTests` now mounts both a Codex menu and an actual `CodexContextMenu.Open(target)` popup to verify submenu KeyDown open/close, nested child focus transfer, sibling focus traversal, focus wrap, non-menu item skipping, loading suppression, and rendered frame capture through the template/popup path.
- Agent B2 pointer submenu pass added shared pointer hover timing for `CodexMenuItem` and `CodexContextMenuItem`: 100ms submenu open delay, 300ms close delay, owner close-request cancellation when moving into submenu children, timer cleanup on detach, and synchronous keyboard focus transfer with a Dispatcher fallback for generated popup children. Source guards now cover the pointer open/close hooks in both menu item classes and the shared `PointerSubMenuState` timer implementation.
- Docs Agent A8 expanded the state-example model from ten high-risk pages to every current component/token page except the overview. The Docs gallery now has 40 standalone `*States.axaml` samples across Forms, Feedback, Navigation, Overlay, Data Display, and Tokens; each is registered as a `DocsExampleCase`, renders under the component preview, and exposes its own local `Show code` / `Hide code` inline `DocsCodeBlock`.
- Agent B2 close-on-select pass added a shared menu selection close chain for `CodexMenuItem` and `CodexContextMenuItem`. Leaf selection now closes open parent submenus and closes the owning `CodexContextMenu` popup, while submenu triggers, disabled items, loading roots, and command-blocked items stay open. Mounted headless tests cover direct context-menu item selection, submenu leaf selection, and submenu trigger non-close behavior.
- Docs Agent A10 expanded the desktop Docs data-display coverage with dedicated `CodexPinnedTable` and ECharts `CsUsageTrendChart` pages. Each page has a default preview, a standalone state AXAML sample, local `Show code` / `Hide code` expansion under the rendered component, and state/event matrix entries for pinned scroll sync, transition-key refresh motion, chart data refresh, empty state, refresh overlay, and pointer tooltip behavior.
- Docs Agent A11 expanded the Layout Docs coverage with a dedicated `Sidebar primitives` page and added a `Table anatomy` example under Data Display. It separates header/content/footer/group/action/menu-button/menu-action/badge/submenu examples from the full application-shell page, adds default and state AXAML samples, covers `CodexTableBody`/`CodexTableFooter`/`CodexTableCaption`, and keeps each example's local `Show code` expansion under the rendered component.
- Docs Agent A12 expanded the Navigation Docs anatomy coverage with standalone `Menu anatomy` and `Context menu anatomy` examples. They cover grouped menu items, `CodexMenuEmpty`, `CodexMenuLoading`, `CodexContextMenuGroup`, `CodexContextMenuShortcut`, inset rows, submenu placement, disabled leaves, and loading suppression while preserving the component-local `Show code` expansion contract.
- Docs Agent A13 added a dedicated `Image icon` page for `CodexImageIcon`. The Docs project now links the app provider PNG assets as portable Avalonia resources, default/state examples use `avares://CodexSwitchUI.Docs/Assets/icons/...` paths, and rendered tests verify the linked resources load into real `CodexImageIcon.Source` values.
- Docs Agent A14 added a standalone `Command anatomy` example under Navigation. It explicitly covers `CodexCommandInput`, `CodexCommandList`, `CodexCommandGroup`, `CodexCommandItem`, `CodexCommandLoading`, and `CodexCommandEmpty`, with rendered tests verifying the standalone input and loading/empty rows mount in the real Docs page.
- Agent C24.1 started the legacy button migration with destructive confirmation dialogs. `DeleteModelDialog` and `DeleteProviderDialog` now use `CodexButton` for cancel/delete actions, mapping old `outline compact` to `Variant=Outline Size=Small` and old `destructive compact` to `Variant=Destructive Size=Small` while preserving commands, i18n content, layout, and the existing dialog shell.
- Agent C24.2 migrated the `SettingsPage` action buttons from legacy `CsButton`/`CsIconButton` to `CodexButton`/`CodexIconButton`. The pass covers the back icon, Apply, Save, update check, open downloaded update, and open releases actions, preserving commands and bindings while mapping old compact/outline styling to Codex size and variant properties.
- Agent C24.3 migrated the `ClaudePage` action buttons from legacy `CsButton` to `CodexButton`. The pass covers back-to-providers, provider row set-active/edit actions, Claude model shortcut chips, and the save-settings action with `CodexButton.LeadingIcon`, preserving all commands, command parameters, and bindings.
- Agent C24.4 migrated the small dialog action surfaces in `ModelEditorDialog` and `CodexAuthImportDialog` from legacy `CsButton`/`CsIconButton` to `CodexButton`/`CodexIconButton`. Close actions use the ghost icon-button treatment, cancel actions map to `Variant=Outline Size=Small`, save/import actions use `Size=Small` plus `CodexButton.LeadingIcon`, and all commands/tooltips/i18n content remain bound to the original view model paths.

Next recommended slice:

- Continue Agent B2 only for remaining native popup edge cases such as deeper pointer travel between native popup surfaces; popup close/focus lifecycle, disabled/loading menu item activation, close-on-select propagation, submenu key gates, pointer submenu delay hooks, nested submenu focus transfer, mounted menu traversal, and mounted context-menu popup focus wrap now have coverage.
- Continue Docs Agent A7 by adding persisted PNG artifact generation for manual review, or continue expanding fingerprints from multi-case pages to every standalone Docs page. The high-risk multi-case pages now have comparable baseline coverage.
- Continue Motion Agent M4 with a Docs screenshot route that captures motion-state examples before claiming animation parity.

### Agent C45: Toggle And ToggleGroup Core Components

Scope:

- Add `CodexToggle`, `CodexToggleGroup`, and `CodexToggleGroupItem` under `CodexSwitchUI/src/CodexSwitchUI/Controls`.
- Add `Themes/Controls/Toggle.axaml` and include it from `ComponentStyles.axaml`.
- Add Docs page `forms.toggle` with standalone AXAML samples: default, states, group, and interaction.
- Update docs-site Forms coverage in English and Chinese.

Web evidence:

- shadcn Toggle is a two-state button with default/outline styling, text/icon examples, size examples, disabled examples, and RTL coverage.
- Radix Toggle exposes pressed/defaultPressed/onPressedChange/disabled and documents Space/Enter activation with data-state on/off.
- shadcn Toggle Group exposes single/multiple groups, outline, size, spacing, vertical, disabled, custom, and RTL examples; its 2026-05-17 changelog changed default spacing to `2`.
- Radix Toggle Group exposes single/multiple `value`, `onValueChange`, `disabled`, `rovingFocus`, `orientation`, `loop`, item `value`, data-state on/off, and arrow/Home/End keyboard navigation.

Implementation plan:

- Use `ToggleButton` as the native pressed-state base for `CodexToggle`, syncing `pressed`, `state-on`, `state-off`, variant, size, disabled, pointer, focus-visible, and checked classes.
- Use `ItemsControl` as the group root, with single/multiple selection normalization, `SelectedValue`, `SelectedValues`, `ValueChanged`, disabled item skipping, orientation classes, roving focus, loop control, and 8px default item spacing for shadcn spacing-2 parity.
- Keep the style tokenized with dynamic CodexSwitch color, radius, disabled opacity, and motion resources; avoid Fluent/BasedOn default chrome.
- Reuse Docs `BuildInlineExample` so every rendered case places a local `Show code` / `Hide code` button below the component and expands that exact AXAML file.

Verification:

- Target `ComponentStructureTests`, `FormComponentDetailTests`, `DocsPanelLayoutTests`, rendered Docs lifecycle tests, visual snapshot update/compare, docs-site lint/build, full `CodexSwitchUI.Tests`, outer `CodexSwitch.Tests`, and `git diff --check`.

Non-goals:

- Do not migrate existing app pages to Toggle in this slice.
- Do not replace `CodexSwitch` or `CodexSegmentedControl`; Toggle and ToggleGroup cover a different Web component contract.
- Dispatch Agent C21 to migrate the next legacy control family in active views, with the strongest candidates being `CsButton` action surfaces, `CsSwitch` toggles, or page-local empty/no-result surfaces to `CodexEmptyState`.
- Add screenshot-backed gallery states for the completed form, command, hover-card, and overlay controls before claiming visual parity for those families.

## Agent Dispatch Queue

Use these as concrete sub-agent work packets. Each packet should inspect first, then patch, then verify. Agents must not touch unrelated app business files or localization files.

### Agent B1: Keyboard And Open-State Semantics

Status: first pass completed for tabs, navigation menu, and collapsible. Menus/context menus still rely on Avalonia native menu keyboard behavior and need a deeper rendered/headless interaction pass before being called Web-parity complete.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexTabs.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNavigationMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexContextMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCommand.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCollapsible.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`

Evidence to gather:

- Web behavior for tab selection, menu item activation, command item active state, escape close, enter/space activation, and pointer hover/open states.
- Current Avalonia overrides for `OnKeyDown`, `OnPointerPressed`, `OnGotFocus`, and open-state class sync.

Implementation targets:

- Add focused tests for Enter, Space, Escape, and arrow-key behavior where the current controls already implement or should implement parity.
- Ensure pointer hover never mutates selection unless Web behavior opens a hover disclosure, such as navigation menu viewport.
- Ensure `open`, `closed`, `active`, and `selected` classes are property-driven and observable in tests.

Verification:

- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `dotnet build CodexSwitch/CodexSwitch.csproj`

### Agent A2: Motion Token Coverage Audit

Status: completed for tokenizable component and primitive style transitions. The only remaining `Duration="0:0:0.*"` values are documented `CrossFade` and `PageSlide` page-transition primitives.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/*.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Primitives/*.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Tokens/BaseTokens.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/CodexSwitchThemeManager.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence to gather:

- All remaining hard-coded `Duration="0:0:0.*"` values.
- Whether each duration is a tokenizable Avalonia transition or a plain `TimeSpan` property like `CrossFade.Duration`.

Implementation targets:

- Replace tokenizable hard-coded durations with `CodexSwitch.MotionDurationFast`, `CodexSwitch.MotionDurationDefault`, or `CodexSwitch.MotionDurationSlow`.
- Add or update tests that fail only for tokenizable hard-coded transitions.
- Document non-tokenizable Avalonia page-transition durations until a wrapper exists.

Verification:

- `rg 'Duration="0:0:0\\.' CodexSwitchUI/src/CodexSwitchUI/Themes/Controls`
- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`

### Motion Agent M1: Runtime Table Refresh Motion

Status: completed. Runtime table refresh animations now consume the same theme motion resources as AXAML component styles.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexTable.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- Web baseline `.cs-hover-link` uses 160ms `ease` transitions for border/background/transform and a restrained `translateY(-1px)` hover affordance.
- Existing Avalonia motion tokens expose `CodexSwitch.MotionDurationDefault`, `CodexSwitch.MotionEaseOut`, and reduced-motion zero-duration resources.
- `CodexTable` and `CodexPinnedTable` still used runtime hard-coded `TimeSpan.FromMilliseconds(150)`, `TimeSpan.FromMilliseconds(160)`, and `new CubicEaseOut()` outside the AXAML style-duration guard tests.

Implementation targets:

- Resolve runtime table transition duration/easing from `CodexSwitchResourceKeys.MotionDurationDefault` and `CodexSwitchResourceKeys.MotionEaseOut`.
- Refresh runtime transition objects when `TransitionKey` changes so theme/reduced-motion changes are respected after a control is already mounted.
- When duration is `TimeSpan.Zero`, set final opacity/translate state immediately and skip the initial visual dip.
- Add source-level guard coverage so `CodexTable.cs` cannot reintroduce hard-coded 150/160ms table transition literals or local `CubicEaseOut` construction.

Verification:

- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `dotnet build CodexSwitch/CodexSwitch.csproj`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0`
- `git -C CodexSwitchUI diff --check`

### Motion Agent M2: Runtime Animation Parity Audit

Status: completed for the targeted runtime tokenization pass. Shared runtime token resolution, Table refresh, Collapsible open/close height animation, Sonner enter/exit timing, UsagePieChart reveal duration, Skeleton pulse/shimmer duration, and the explicit timing allowlist are now covered. Rendered motion verification remains a follow-up Motion Agent M3 task.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSonner.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCollapsible.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSkeleton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexUsagePieChart.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexHoverCard.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexScrollArea.cs`

Evidence to gather:

- Which values are Web-parity motion durations for enter/show/close/hide/loading state.
- Which values are non-token delays or frame intervals, such as hover open delay, scroll idle delay, and 16ms frame timers.
- Current tests that cover reduced motion and lifecycle state transitions.
- Final M2 classification: 16ms timers are frame scheduling, hover-card 700/300ms values are Radix-style behavior delays, scroll idle 650ms is scrollbar visibility behavior, Sonner 4s default duration is toast lifetime behavior, and Table/Collapsible/Sonner/UsagePieChart/Skeleton durations are token-backed Web-parity motion.

Implementation targets:

- Tokenize runtime enter/show/close/hide durations where they represent component motion. Status: completed for `CodexTable`, `CodexPinnedTable`, `CodexCollapsible`, `CodexSonnerService`, `CodexUsagePieChart`, and `CodexSkeleton`.
- Keep frame intervals and intentional Web delays documented as non-tokenizable timing constants. Status: completed with `RuntimeTimingConstantsHaveExplicitMotionClassification`.
- Add tests that prevent Web-parity runtime motion from using local duration/easing literals. Status: completed for table runtime transitions, Collapsible open/close duration, Sonner enter/exit duration, UsagePieChart reveal duration, and Skeleton pulse duration.
- Add reduced-motion checks for any show/close animation that can leave a control in an intermediate opacity/transform state. Status: covered through zero-duration paths for Table, Sonner, UsagePieChart, and dynamic-resource duration binding for Collapsible/Skeleton.
- Continue with rendered motion verification in Motion Agent M3 before claiming full animation parity.

Verification:

- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `dotnet build CodexSwitch/CodexSwitch.csproj`

### Motion Agent M3: Rendered Motion Verification

Status: second pass completed for headless rendered lifecycle checks. Remaining work is screenshot-backed Docs examples for motion states.

Scope:

- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexTable.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCollapsible.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSonner.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSkeleton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexUsagePieChart.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexHoverCard.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexScrollArea.cs`

Evidence gathered:

- Existing rendered test harness uses `HeadlessUnitTestSession`, `CodexSwitchTheme`, and `CaptureRenderedFrame`.
- Reduced-motion final states must be verified after controls are mounted and templated, not only by source scans.
- Repeated headless sessions exposed mutable static chart fallback brushes as a real render-lifecycle bug; those fallbacks now use immutable brushes.

Implementation targets:

- Verify reduced-motion runtime surfaces render final states in a mounted tree. Status: completed for table, collapsible, sonner, skeleton, and usage pie chart.
- Verify normal-motion mounted surfaces expose nonzero tokenized transitions or entering/reveal start states. Status: completed for table, sonner, skeleton, and usage pie chart.
- Capture a rendered frame during the mounted motion verification so the test covers compositor/render path. Status: completed.
- Verify Web behavior delays and visibility timing in mounted controls. Status: completed for HoverCard open/close delays and ScrollArea hover/scroll visibility classes.
- Continue with screenshot-backed Docs motion-state examples.

Verification:

- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --filter MotionRenderedLifecycleTests`
- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`

### Agent C1: Missing Core Component Shortlist

Status: first missing overlay primitive completed with `CodexTooltip`. Continue as Agent C2 for the next component.

Scope:

- `docs-site/app`
- `CodexSwitchUI/src/CodexSwitchUI/Controls`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence to gather:

- Web-side primitives/components present in docs or app pages but absent from `CodexSwitchUI`.
- Current component style architecture and naming conventions.

Implementation targets:

- Pick the next smallest missing high-value component from hover card, dropdown button, split button, scroll area wrapper, empty state, or pagination.
- Add class file, style file, theme include, template parts, motion, state classes, and tests.

Verification:

- Component structure tests must prove class, style, theme include, template, motion, disabled state, and selector scoping.

### Agent C2: Command Dialog Component

Status: completed for `CodexCommandDialog` first pass. It has a class file, style file, theme include, open/closed and loading classes, close-on-select behavior, command loading suppression coverage, docs gallery coverage, and structure/behavior tests.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCommandDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/CommandDialog.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `dotnet build src/CodexSwitchUI.Docs/CodexSwitchUI.Docs.csproj -f net10.0`

### Agent C3: Next Missing Core Component

Status: completed for `CodexHoverCard` first pass. It has a class file, style file, theme include, side and alignment classes, optional arrow, Radix default open/close delays, pointer/focus/Escape open-close methods, docs gallery coverage, and structure/behavior tests.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexHoverCard.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/HoverCard.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `dotnet build src/CodexSwitchUI.Docs/CodexSwitchUI.Docs.csproj -f net10.0`

### Agent C4: Next Missing Core Component

Status: completed for `CodexDropdownButton` first pass. It has a class file, style file, theme include, CodexButton trigger composition, popup content surface, light dismiss, Escape dismiss, side and alignment classes, optional arrow, loading suppression, close-on-select behavior, docs gallery coverage, and structure/behavior tests.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDropdownButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/DropdownButton.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs-site/content/docs/*/ui-system/navigation.mdx`

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `dotnet build src/CodexSwitchUI.Docs/CodexSwitchUI.Docs.csproj -f net10.0 -p:BuildProjectReferences=true`

### Agent C5: Next Missing Core Component

Status: completed for `CodexSplitButton` first pass. It has a class file, style file, theme include, CodexButton primary action and menu trigger composition, joined corner radii, popup content surface, light dismiss, Escape dismiss, side and alignment classes, optional arrow, loading suppression, command CanExecute suppression, close-on-select behavior, docs gallery coverage, and structure/behavior tests.

Completed files:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSplitButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/SplitButton.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Converters/SplitButtonCornerRadiusPartConverter.cs`

### Agent C6: Next Missing Core Component

Status: completed for `CodexPagination` first pass. It has a class file, style file, theme include, 1-based page selection, boundary/sibling ellipsis item generation, current/ellipsis/first/last/compact/loading state classes, first/previous/next/last action buttons, loading/disabled gates, Home/End/Left/Right/PageUp/PageDown keyboard navigation, docs gallery coverage, docs-site coverage, and structure/behavior tests.

Completed files:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexPagination.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Pagination.axaml`

### Agent C7: Next Missing Core Component

Status: completed for `CodexScrollArea` first pass. It has a class file, style file, theme include, scoped ScrollViewer and ScrollBar templates, Web-style scrollbar thumb visuals, `Auto`/`Always`/`Hover`/`Scroll` type classes, active scrolling state, inset-content mode, offset/extent/viewport metrics, can-scroll and boundary state classes, scroll changed forwarding, docs gallery coverage, docs-site coverage, and structure/behavior tests.

Completed files:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexScrollArea.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ScrollArea.axaml`

### Agent C8: Next Missing Core Component

Status: completed for `CodexEmptyState` first pass. It has a class file, style file, theme include, icon/header/content/action slots, primary and secondary action events, command `CanExecute` tracking, loading and disabled trigger suppression, variant and size state classes, docs gallery coverage, docs-site coverage, and structure/behavior tests.

Scope:

- `docs-site/content/docs/*/ui-system`
- `CodexSwitchUI/src/CodexSwitchUI/Controls`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests`

Evidence to gather:

- Which component is most visible in current app/gallery needs: split button, scroll area wrapper, empty state, or pagination.
- Existing Avalonia primitives that should be wrapped instead of rewritten.
- Web styling and event expectations for hover/open, keyboard activation, Escape close, disabled state, and focus return.

Implementation targets:

- Add exactly one component per pass with class file, style file, theme include, state classes, tokenized motion, and tests.
- Prefer composition over special page-local code.
- Do not migrate app pages in the same pass unless the component is unusable without one representative usage.

Verification:

- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `dotnet build CodexSwitch/CodexSwitch.csproj`

Completed files:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexEmptyState.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/EmptyState.axaml`

### Agent C9: Next Missing Core Component Or Representative Migration

Status: completed for `CodexField` first pass. It has a class file, style file, theme include, label/description/message/required slots, intent and size classes, disabled opacity token coverage, docs gallery examples, docs-site forms notes, and structure/state tests. Representative app migration is now underway through later C10/C11 passes.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `docs-site/content/docs/*/ui-system`
- one app view only if it provides a representative empty/no-result usage without touching unrelated business logic.

Evidence to gather:

- Remaining Web primitives or repeated page-local empty/loading patterns still absent from `CodexSwitchUI`.
- Current app usages of ad hoc empty states, disabled retry actions, and no-result content.
- Whether the next pass is safer as a new primitive, a gallery screenshot pass, or one contained migration.

Implementation targets:

- Either add one missing component with the same class/style/theme/test contract, or migrate one representative page-local empty/no-result surface to `CodexEmptyState`.
- Keep event semantics Web-aligned: pointer hover is visual only, loading suppresses command/event activation, and disabled actions never fire.
- Update docs and tests in the same pass.

Verification:

- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `dotnet build CodexSwitch/CodexSwitch.csproj`

### Agent C10: Representative Field Migration

Status: first pass completed for `AddProviderPage`. The seven provider form field wrappers now use `CodexField` through a separate `CodexSwitchUI.Controls` namespace, while old input/select controls and all bindings remain unchanged.

Scope:

- `CodexSwitch/Views/Pages/AddProviderPage.axaml`
- Legacy `ui:CsField` wrappers around provider name, note, website, API key, base URL, model name, and protocol.

Evidence gathered:

- `AddProviderPage.axaml` was clean before this pass and contained seven `ui:CsField` usages.
- Other app pages already consume `CodexSwitchUI.Controls` and `CodexField`, so the application has the new library reference and theme path needed for this migration.
- The page still carries many old `Cs*` controls, so this pass intentionally adds a second namespace and migrates only field wrappers.

Implementation targets:

- Replace only `<ui:CsField>` wrappers with `<cui:CodexField>`.
- Preserve `Grid.Row`, `Grid.Column`, `Grid.ColumnSpan`, `Label`, child controls, bindings, placeholders, and command behavior.
- Leave broader `CsInput`, `CsSelect`, button, badge, switch, and page layout migration for later Agent D/C11 passes.

Verification:

- `dotnet build CodexSwitch/CodexSwitch.csproj`
- `git diff --check`

### Agent C11: Settings Field Migration

Status: first pass completed for `SettingsPage`. The settings form field wrappers now use `CodexField` through a separate `CodexSwitchUI.Controls` namespace, while old input/select/segmented controls and all bindings remain unchanged.

Scope:

- `CodexSwitch/Views/Pages/SettingsPage.axaml`
- Legacy `ui:CsField` wrappers in proxy host/port, outbound proxy mode/custom URL, outbound connection HTTP version/connect timeout, resilience failure/recovery delay, inbound API key, and usage pricing fields.

Evidence gathered:

- `SettingsPage.axaml` was clean before this pass and contained thirteen `ui:CsField` usages.
- The target page is a high-value app surface because settings forms exercise text input, select, segmented control, visibility-bound sections, and compact grid layouts.
- The page still uses many old `Cs*` controls, so the migration intentionally keeps a second namespace and changes only field wrappers.

Implementation targets:

- Replace only `<ui:CsField>` wrappers with `<cui:CodexField>`.
- Preserve child controls, bindings, commands, section visibility, `Grid.Row`, `Grid.Column`, `Grid.ColumnSpan`, placeholders, and tab behavior.
- Leave `CsSection`, `CsInput`, `CsSelect`, `CsSegmentedControl`, switches, buttons, and broader settings layout migration for later Agent D/C12 passes.

Verification:

- `dotnet build CodexSwitch/CodexSwitch.csproj`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0`
- `git diff --check`

### Agent C12: Provider Editor Field Migration

Status: first pass completed for `ProviderEditorDialog`. The provider editor dialog now uses `CodexField` for every form field wrapper, including fields inside model route and model conversion data templates.

Scope:

- `CodexSwitch/Views/Dialogs/ProviderEditorDialog.axaml`
- Legacy `ui:CsField` wrappers in connection details, protocol routing, usage query request/response mapping, model route rows, and model conversion rows.

Evidence gathered:

- `ProviderEditorDialog.axaml` was clean before this pass and contained thirty-two `ui:CsField` usages.
- This dialog is the largest remaining clean field cluster and exercises nested grids, `ItemsControl` row templates, text input, select, textarea, readonly/disabled child states, and command-bound remove buttons.
- The dialog still uses old `Cs*` primitives elsewhere, so this migration intentionally keeps `CodexField` isolated behind a second namespace.

Implementation targets:

- Replace only `<ui:CsField>` wrappers with `<cui:CodexField>`.
- Preserve child controls, bindings, commands, command parameters, row templates, read-only/enabled child states, placeholders, grid positioning, and dialog open/save/cancel behavior.
- Leave `CsDialog`, `CsButton`, `CsInput`, `CsSelect`, `CsTextarea`, switches, route row styling, and broader dialog layout migration for later Agent D/C13 passes.

Verification:

- `dotnet build CodexSwitch/CodexSwitch.csproj`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0`
- `git diff --check`

### Agent C13: Final Field Wrapper Migration

Status: completed for remaining `CsField` wrappers in active views. `ModelEditorDialog` and `ClaudePage` now use `CodexField`, and `rg '<ui:CsField' CodexSwitch/Views` returns no matches.

Scope:

- `CodexSwitch/Views/Dialogs/ModelEditorDialog.axaml`
- `CodexSwitch/Views/Pages/ClaudePage.axaml`
- Remaining legacy `ui:CsField` wrappers in model metadata, input/cache pricing, output/multiplier pricing, and Claude model selection.

Evidence gathered:

- Before this pass, only `ModelEditorDialog.axaml` and `ClaudePage.axaml` still contained `ui:CsField`.
- Both files were clean before this pass.
- The fields were simple label/content wrappers around existing controls, so this migration could preserve all inner controls and bindings.

Implementation targets:

- Replace only `<ui:CsField>` wrappers with `<cui:CodexField>`.
- Preserve inner `CsInput`, buttons, items controls, bindings, placeholders, dialog commands, and page behavior.
- Keep broader `CsDialog`, `CsInput`, `CsButton`, `CsSelect`, and page layout migration for later Agent D/C14 passes.

Verification:

- `rg '<ui:CsField|</ui:CsField>' CodexSwitch/Views`
- `dotnet build CodexSwitch/CodexSwitch.csproj`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0`
- `git diff --check`

### Agent C14: Model Editor Input Migration

Status: first pass completed for `ModelEditorDialog`. The dialog now uses `CodexTextBox` for every model metadata and pricing input inside `CodexField`.

Scope:

- `CodexSwitch/Views/Dialogs/ModelEditorDialog.axaml`
- Legacy `ui:CsInput` controls in model metadata, input/cache pricing, output pricing, and fast multiplier override fields.

Evidence gathered:

- `CodexTextBox` inherits Avalonia `TextBox`, so existing `Text` and `PlaceholderText` bindings stay compatible.
- `CodexTextBox` adds CodexSwitchUI-owned Web behavior: intent/size classes, read-only class sync, pointer-suppressed focus-visible, and tokenized input transitions.
- `CsInput` only adds the legacy `cs-input` class over a native `TextBox`, so the migration can preserve model editor bindings and layout.

Implementation targets:

- Replace only the `ModelEditorDialog` `ui:CsInput` controls with `cui:CodexTextBox`.
- Preserve `CodexField` wrappers, bindings, placeholders, dialog commands, pricing note content, and save/cancel behavior.
- Leave `CsDialog`, `CsButton`, badges, section chrome, and broader app input/select migration for later Agent D/C15 passes.

Verification:

- `rg '<ui:CsInput' CodexSwitch/Views/Dialogs/ModelEditorDialog.axaml`
- `dotnet build CodexSwitch/CodexSwitch.csproj`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0`
- `git diff --check`

### Agent C15: Add Provider Form Input Migration

Status: first pass completed for the `AddProviderPage` provider creation form. The form now uses `CodexTextBox` and `CodexSelect` inside the previously migrated `CodexField` wrappers.

Scope:

- `CodexSwitch/Views/Pages/AddProviderPage.axaml`
- Legacy `ui:CsInput` and `ui:CsSelect` controls in the provider name, note, website, API key, base URL, default model, and protocol fields.

Evidence gathered:

- The target field cluster was already migrated to `CodexField`, so replacing its child controls moves actual form behavior to CodexSwitchUI without changing surrounding page layout.
- `CodexTextBox` preserves `Text`, `PlaceholderText`, `PasswordChar`, and `UpdateSourceTrigger` bindings from the old `CsInput` usage.
- `CodexSelect` preserves `ItemsSource` and `SelectedItem` bindings from the old `CsSelect` usage while adding CodexSwitchUI-owned popup/focus-visible styling.

Implementation targets:

- Replace only the provider creation form child controls with `cui:CodexTextBox` and `cui:CodexSelect`.
- Preserve all provider bindings, password masking, placeholders, grid positioning, and save behavior.
- Leave OAuth account row editing, buttons, switches, badges, image icons, and broader AddProviderPage row/card migration for later Agent D/C16 passes.

Verification:

- `rg '<ui:CsInput|<ui:CsSelect' CodexSwitch/Views/Pages/AddProviderPage.axaml` should show only the intentionally deferred OAuth account row editor.
- `dotnet build CodexSwitch/CodexSwitch.csproj`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0`
- `git diff --check`

### Agent C16: Provider Editor Core Input Migration

Status: first pass completed for the `ProviderEditorDialog` connection and protocol routing sections. These core provider editor fields now use `CodexTextBox` and `CodexSelect` inside the previously migrated `CodexField` wrappers.

Scope:

- `CodexSwitch/Views/Dialogs/ProviderEditorDialog.axaml`
- Legacy `ui:CsInput` and `ui:CsSelect` controls in provider name, default model, note, website, base URL, API key, protocol, and service tier.

Evidence gathered:

- The target fields were already migrated to `CodexField`, making the child-control migration a focused move from legacy visual style to CodexSwitchUI form behavior.
- `CodexTextBox` preserves the existing `Text`, `PasswordChar`, `PlaceholderText`, and `UpdateSourceTrigger` bindings.
- `CodexSelect` preserves the existing `ItemsSource` and `SelectedItem` protocol bindings while adding CodexSwitchUI-owned select popup and focus-visible states.

Implementation targets:

- Replace only the connection/protocol-routing child controls with `cui:CodexTextBox` and `cui:CodexSelect`.
- Preserve provider bindings, password masking, placeholders, grid placement, switch settings, save/cancel behavior, and existing dialog sections.
- Leave usage-query mappings, model route rows, model conversion rows, switches, buttons, and dialog chrome for later Agent D/C17 passes.

Verification:

- `rg '<ui:CsInput|<ui:CsSelect|<ui:CsTextarea' CodexSwitch/Views/Dialogs/ProviderEditorDialog.axaml` should no longer show the connection/protocol routing section.
- `dotnet build CodexSwitch/CodexSwitch.csproj`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0`
- `git diff --check`

### Agent C17: Provider Editor Usage Query Input Migration

Status: completed for the `ProviderEditorDialog` usage-query cluster. The request and response mapping fields now use CodexSwitchUI form primitives; the dynamic model route/conversion row templates were completed in Agent C18.

Scope:

- `CodexSwitch/Views/Dialogs/ProviderEditorDialog.axaml`
- Legacy `ui:CsInput`, `ui:CsSelect`, and `ui:CsTextarea` controls in the usage query method, timeout, URL, headers, body, and JSON path mapping fields.

Evidence gathered:

- The target cluster was already wrapped in `CodexField` from Agent C12, so child-control migration could stay focused and preserve layout.
- `CodexTextBox` preserves the existing text, placeholder, and `UpdateSourceTrigger` bindings.
- `CodexTextarea` preserves multiline headers/body bindings while moving focus-visible styling and textarea motion to CodexSwitchUI.
- `CodexSelect` preserves method `ItemsSource` and `SelectedItem` binding while adding CodexSwitchUI-owned popup and item state styling.

Implementation targets:

- Replace only usage-query child controls with `cui:CodexTextBox`, `cui:CodexSelect`, and `cui:CodexTextarea`.
- Preserve usage query enablement, templates, test command, result text, grid placement, i18n labels, and all mapping bindings.
- Leave model route and model conversion row controls for a later row-template pass because those rows have separate add/remove and conversion behavior.

Verification:

- `rg '<ui:CsInput|<ui:CsSelect|<ui:CsTextarea' CodexSwitch/Views/Dialogs/ProviderEditorDialog.axaml` should show only dynamic model route/conversion rows.
- `dotnet build CodexSwitch/CodexSwitch.csproj`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0`
- `git diff --check`

### Agent C18: Provider Editor Dynamic Row Input Migration

Status: completed for the deferred `ProviderEditorDialog` dynamic model route and model conversion row templates. The provider editor now has no remaining `CsInput`, `CsSelect`, or `CsTextarea` usage.

Scope:

- `CodexSwitch/Views/Dialogs/ProviderEditorDialog.axaml`
- `CodexSwitch.Tests/AppViewMigrationTests.cs`
- Legacy `ui:CsInput` and `ui:CsSelect` controls inside `ModelRows` and `ModelConversionRows` data templates.

Evidence gathered:

- Before this pass, `ProviderEditorDialog.axaml` still had seven legacy form controls: model id, upstream model, row protocol, display name, service tier, conversion source model, and conversion target model.
- `CodexTextBox` preserves `Text`, `PlaceholderText`, `IsReadOnly`, `IsEnabled`, and `UpdateSourceTrigger` bindings while adding CodexSwitchUI focus-visible and tokenized input behavior.
- `CodexSelect` preserves `ItemsSource` and `SelectedItem` bindings for row protocol selection while moving popup/item styling to CodexSwitchUI.

Implementation targets:

- Replace only model route/conversion row `ui:CsInput` and `ui:CsSelect` controls with `cui:CodexTextBox` and `cui:CodexSelect`.
- Preserve row add/remove commands, command parameters, row templates, read-only default conversion source rows, target edit gating, placeholders, and switch behavior.
- Add a markup regression test that rejects legacy form inputs returning to the provider editor dialog.

Verification:

- `rg '<ui:CsInput|<ui:CsSelect|<ui:CsTextarea' CodexSwitch/Views/Dialogs/ProviderEditorDialog.axaml`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --filter AppViewMigrationTests`
- `dotnet build CodexSwitch/CodexSwitch.csproj`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0`
- `git diff --check`

### Agent C19: Settings Page Form Input Migration

Status: completed for the `SettingsPage` language selector, route/network settings, inbound auth key, resilience settings, and usage pricing fields. The settings page now has no remaining `CsInput`, `CsSelect`, or `CsTextarea` usage.

Scope:

- `CodexSwitch/Views/Pages/SettingsPage.axaml`
- `CodexSwitch.Tests/AppViewMigrationTests.cs`
- Legacy `ui:CsInput` and `ui:CsSelect` controls in general language, proxy host/port, outbound proxy URL, outbound HTTP version, connect timeout, circuit breaker values, inbound API key, and usage pricing fields.

Evidence gathered:

- Before this pass, `SettingsPage.axaml` still had one language `CsSelect`, one HTTP-version `CsSelect`, and eleven `CsInput` controls across route, auth, resilience, and usage tabs.
- `CodexSelect` preserves `ItemsSource`, `SelectedItem`, `ItemTemplate`, alignment, and width settings while moving popup/item/focus-visible behavior to CodexSwitchUI.
- `CodexTextBox` preserves text and placeholder bindings while moving input focus-visible styling and tokenized motion to CodexSwitchUI.

Implementation targets:

- Replace only settings page `ui:CsInput` and `ui:CsSelect` controls with `cui:CodexTextBox` and `cui:CodexSelect`.
- Preserve tab visibility, settings sections, segmented controls, switches, save/apply commands, item templates, placeholders, and all business bindings.
- Add a markup regression test that rejects legacy form inputs returning to the settings page.

Verification:

- `rg '<ui:CsInput|<ui:CsSelect|<ui:CsTextarea' CodexSwitch/Views/Pages/SettingsPage.axaml`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --filter AppViewMigrationTests`
- `dotnet build CodexSwitch/CodexSwitch.csproj`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0`
- `git diff --check`

### Agent C20: Final Active-View Form Input Migration

Status: completed for the last active-view `CsInput`, `CsSelect`, and `CsTextarea` usages. Active app views now use CodexSwitchUI form input controls for text input, textarea, and select surfaces.

Scope:

- `CodexSwitch/Views/Dialogs/CodexAuthImportDialog.axaml`
- `CodexSwitch/Views/Pages/ClaudePage.axaml`
- `CodexSwitch/Views/Pages/AddProviderPage.axaml`
- `CodexSwitch.Tests/AppViewMigrationTests.cs`

Evidence gathered:

- Before this pass, the only remaining active-view legacy form inputs were `CodexAuthImportDialog` JSON `CsTextarea`, `ClaudePage` model `CsInput`, and `AddProviderPage` OAuth account display-name `CsInput`.
- `CodexTextarea` preserves multiline text, placeholder, and minimum-height behavior while moving focus-visible and textarea motion to CodexSwitchUI.
- `CodexTextBox` preserves simple text bindings and placeholders while moving focus-visible and input motion to CodexSwitchUI.

Implementation targets:

- Replace the final three active-view `ui:CsInput`/`ui:CsTextarea` controls with `cui:CodexTextBox`/`cui:CodexTextarea`.
- Preserve import/cancel commands, model shortcut buttons, OAuth account save/switch/delete commands, row layout, placeholders, and all bindings.
- Expand migration tests from page-specific checks to a global active-view invariant rejecting `CsInput`, `CsSelect`, and `CsTextarea` in any `CodexSwitch/Views/**/*.axaml` file.

Verification:

- `rg '<ui:CsInput|<ui:CsSelect|<ui:CsTextarea' CodexSwitch/Views`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --filter AppViewMigrationTests`
- `dotnet build CodexSwitch/CodexSwitch.csproj`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0`
- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `git diff --check`

### Docs Agent A1: Docs Shell And Code Sample Architecture

Status: initial rewrite completed. The docs app now has a categorized navigation shell, independent page records, standalone copied AXAML examples, a source rail, and a Monaco-like code block control. More pages still need full component-specific content in Docs Agent A2.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Docs/DocsPage.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Docs/DocsCodeSamples.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Controls/DocsCodeBlock.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/**`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- The goal requires the docs design to be rewritten, categories to be clearly separated, every menu item to be an independent page, and examples to have standalone AXAML source files.
- The docs app is an operational component workbench, so the layout should prioritize navigation, preview workspace, and source inspection over marketing-style hero/card layouts.
- The previous tests asserted old workbench methods, so they needed to be rewritten around the new architecture rather than preserving stale strings.

Implementation targets:

- Build a three-column docs shell: category/page navigation, main page preview, and right-side source rail.
- Model each menu item as a `DocsPage` with id, category, title, description, sample path, preview builder, and sections.
- Load AXAML examples from `Examples/Axaml` and copy them to output without compiling them as Avalonia resources.
- Provide a dark code block with editor chrome, line numbers, scrollbars, and clipboard copy.
- Update tests to guard the page registry, navigation, source rail, code block, and standalone AXAML samples.

Verification:

- `dotnet build CodexSwitchUI/src/CodexSwitchUI.Docs/CodexSwitchUI.Docs.csproj -f net10.0 -p:BuildProjectReferences=true`
- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `git -C CodexSwitchUI diff --check`

### Docs Agent A2: Expand Independent Component Pages

Status: first pass completed for all currently registered Docs menu items. Each current page now has a unique AXAML sample, real preview builder, behavior notes, and tests guarding that no registered page reuses a placeholder sample path.

Scope:

- All previously placeholder pages in `CodexSwitchUI.Docs`, starting with navigation, overlay, data display, and motion tokens.
- Dedicated AXAML samples under `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/**`.
- Visual/state coverage for completed components in `CodexSwitchUI/src/CodexSwitchUI/Controls`.

Evidence to gather:

- Web component behavior and docs examples for each target component family.
- Current CodexSwitchUI control properties, pseudo-classes/classes, template parts, and tests.
- Existing app usages that need the docs examples to represent real workflows.

Implementation targets:

- Replace placeholder pages with component-specific previews and state matrices. Status: first preview pass completed; deeper state matrices remain for Docs Agent A3.
- Give every page its own AXAML sample file rather than reusing generic placeholders. Status: completed for the current registry.
- Document behavior contracts for hover, pressed, focus-visible, disabled, loading, selected, open/closed, and dismissal events where relevant. Status: behavior notes added to `DocsPage` and rendered in the main page and source rail.
- Keep the docs layout dense and tool-like: navigation, preview, source, and behavior notes. Status: continued from A1.

Verification:

- `dotnet build CodexSwitchUI/src/CodexSwitchUI.Docs/CodexSwitchUI.Docs.csproj -f net10.0 -p:BuildProjectReferences=true`
- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- Render/smoke the docs app and capture screenshots when local UI tooling is available.

### Dark Agent D1: Docs Dark Theme And Runtime Navigation Stability

Status: completed for the Docs regression reported after A2. The docs shell now reads CodexSwitch theme resources for background, card, muted rail, and border surfaces; runtime Dark switches refresh registered shell/page borders without rebuilding the full window; Docs navigation caches page controls to avoid Avalonia transition detach crashes.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Pagination.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- The reported Dark failure matched hard-coded light shell colors in the Docs rewrite (`#F8FAFC`, `#FFFFFF`, `#E2E8F0`, `#F1F5F9`).
- The reported runtime stack trace pointed at replacing `_pageHost.Content` during menu navigation, which detached animated content and triggered an Avalonia transition null reference.
- A manual app run also exposed a Pagination template render crash from `Size="{Binding $parent[controls:CodexPagination].Size}"`, where the binding parser could not resolve `controls:CodexPagination`.

Implementation targets:

- Use `CodexSwitchResourceKeys.BackgroundBrush`, `CardBrush`, `MutedBrush`, and `BorderBrush` for Docs shell surfaces.
- Register themed `Border` surfaces and refresh them after `CodexSwitchThemeManager.Current.Apply(...)`.
- Avoid full tree rebuilds during runtime theme switches.
- Cache docs page and right-rail controls per page and toggle `IsVisible` rather than replacing the host content.
- Remove the invalid Pagination `$parent[controls:CodexPagination]` binding and move page-button sizing into template selectors.

Verification:

- `dotnet build CodexSwitchUI/src/CodexSwitchUI.Docs/CodexSwitchUI.Docs.csproj -f net10.0 -p:BuildProjectReferences=true`
- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `DocsRenderedLifecycleTests.DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent` covers Dropdown, Dialog, Table, Pagination, Dark switch, and Motion navigation in a rendered headless window.

### Docs Agent A3: Deep State Matrix And Screenshot Verification

Status: first pass completed for structured state and event documentation. Screenshot-backed verification remains queued for Docs Agent A4.

Scope:

- All current Docs pages plus any additional component menu pages added later.
- Visual state matrices for default, hover, pressed, focus-visible, disabled, loading, selected, open/closed, error, and empty states where relevant.
- Screenshot-backed verification for light/dark/theme-custom coverage remains for A4.

Implementation targets:

- Add compact state matrices under each page rather than only one primary preview. Status: completed with `DocsStateCase`.
- Add event matrices that describe pointer, keyboard, dismissal, focus, loading, and command paths. Status: completed with `DocsEventCase`.
- Render state/event matrices in the main content and a right-rail event summary next to source code. Status: completed.
- Capture rendered screenshots for key pages and compare against Web docs references where practical. Status: queued for A4.

Verification:

- `dotnet build CodexSwitchUI/src/CodexSwitchUI.Docs/CodexSwitchUI.Docs.csproj -f net10.0 -p:BuildProjectReferences=true`
- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- Local rendered smoke through Docs navigation in light and dark modes.

### Docs Agent A4: Screenshot-Backed Visual Verification

Status: first pass completed with rendered Skia headless smoke coverage for representative Docs pages in light, dark, and custom themes. Web screenshot comparison remains open before claiming full visual parity.

Scope:

- Current Docs pages under `CodexSwitchUI.Docs`.
- Light, dark, and custom theme screenshots for representative pages.
- Canvas/pixel or screenshot smoke where local UI tooling can reliably capture Avalonia windows.

Implementation targets:

- Add a repeatable rendered smoke script or test harness for visual capture. Status: completed in `DocsRenderedLifecycleTests`.
- Capture at least Overview, Button, Select, Dropdown, Dialog, Table, Pagination, and Motion pages in light and dark modes. Status: completed for light, dark, and custom.
- Compare screenshots against Web docs references or record known Avalonia differences before claiming visual parity.

Verification:

- `dotnet build CodexSwitchUI/src/CodexSwitchUI.Docs/CodexSwitchUI.Docs.csproj -f net10.0 -p:BuildProjectReferences=true`
- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `DocsRenderedLifecycleTests.DocsRepresentativePagesRenderScreenshotsAcrossThemes` captures rendered frames for Overview, Button, Select, Dropdown, Dialog, Table, Pagination, and Motion in Light, Dark, and Custom modes.
- The rendered test saves each frame to PNG in memory, verifies the image can be decoded back, checks expected frame dimensions, and samples shell pixels so Dark must render as a dark surface rather than only setting theme state.

### Agent B2: Overlay Close And Dismissal Semantics

Status: popup-native pass completed for `CodexDropdownButton` and `CodexSplitButton` direct popup close/focus restoration, for `CodexMenuItem`/`CodexContextMenuItem` disabled/loading/command-blocked activation, for explicit submenu keyboard open/close gates, for nested submenu focus transfer, and for mounted menu plus mounted context-menu popup traversal/focus-wrap smoke tests. Remaining work is native popup edge cases such as pointer-open submenu delays and close-on-select propagation before claiming complete menu parity.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexPopover.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexHoverCard.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDropdownButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexContextMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexToast.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCommand.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCommandDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Primitives/CodexOverlay.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`

Evidence to gather:

- Local Web/docs usage for overlay, menu, and feedback components.
- Web primitive behavior for Escape close, outside pointer dismissal, focus return, close buttons, and disabled item non-activation.
- Current Avalonia close-command, `IsOpen`, popup, and menu lifecycle behavior.

Implementation targets:

- Add explicit tested close paths for Escape and close button commands where the component owns dismissal.
- Keep focus-return explicit through `RestoreFocusElement`/`RestoreFocusOnDismiss` instead of page-local callbacks.
- Preserve native Avalonia menu/context-menu behavior where it already handles submenu arrows and traversal, but wrap activation gates and state classes so styles do not depend on default chrome.
- Keep regression tests proving disabled or loading overlay actions do not fire.
- Document remaining behavior left to native Avalonia until rendered popup/headless traversal tests cover submenu arrows and menu focus loops.

Verification:

- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `dotnet build CodexSwitch/CodexSwitch.csproj`

Completed rendered files:

- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayRenderedLifecycleTests.cs`

### Agent E1: Visual Gallery State Coverage

Scope:

- `CodexSwitchUI.Docs` or whichever local gallery app owns component examples.
- Completed form controls and navigation/disclosure controls.

Evidence to gather:

- Existing gallery pages/routes.
- Web screenshots or docs-site examples for default, hover, pressed, focus-visible, disabled, loading, selected, open, and error states.

Implementation targets:

- Add gallery examples for the states already implemented.
- Capture or document visual checks before any component family is called parity complete.

Verification:

- Build the gallery target.
- Run a local smoke route/window and capture screenshots when tooling is available.

### Docs Agent A9: Missing Component Example Coverage

Status: completed for the next missing-component pass. Full Web visual parity remains open.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/**`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/IconButton*.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SideNav*.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SegmentedControl*.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/Metric*.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ProviderCard*.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Primitives/**`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Implementation targets:

- Add independent Docs pages and default/state AXAML samples for Application shell, Section, Icon button, Side navigation, Segmented control, Metric/Stat card, Provider card, Typography, Focus ring, and Overlay primitive. Status: completed.
- Keep each rendered example followed by its own inline `Show code` button using the current `DocsExampleCase.SamplePath`. Status: preserved and guarded.
- Extend rendered representative-page smoke coverage across light, dark, and custom themes for several newly added pages. Status: completed.
- Keep the transition-detach crash guarded by cached page hosts and stable topbar controls. Status: preserved.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --filter FullyQualifiedName~DocsRenderedLifecycleTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C21: Active App Switch Migration

Status: completed for the legacy `CsSwitch` migration slice. Broader app-shell, segmented control, button, icon button, and remaining legacy `ui:*` surfaces remain open.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSwitch.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Switch.axaml`
- `CodexSwitch/Views/Pages/AddProviderPage.axaml`
- `CodexSwitch/Views/Pages/ClaudePage.axaml`
- `CodexSwitch/Views/Pages/SettingsPage.axaml`
- `CodexSwitch/Views/Dialogs/ProviderEditorDialog.axaml`
- `CodexSwitch.Tests/AppViewMigrationTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`

Evidence gathered:

- Old `CsSwitch` was only a `CheckBox` with a `cs-switch` class and a template that rendered a track plus `ContentPresenter`.
- New `CodexSwitch` already owned Web-style focus-visible, thumb motion, pressed scale, checked color, size, intent, and disabled states, but ignored `Content`.

Implementation targets:

- Preserve the pure track/thumb switch while adding optional label content so active app migrations do not lose text. Status: completed with `HasContent` and `PART_Content`.
- Replace all remaining active-view `<ui:CsSwitch>` usages with `<cui:CodexSwitch>`. Status: completed for Add Provider, Claude, Settings, and Provider Editor.
- Guard active views from regressing to old `CsSwitch`. Status: completed in `AppViewMigrationTests`.

Verification:

- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --filter FullyQualifiedName~FormComponentDetailTests`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --filter FullyQualifiedName~AppViewMigrationTests`
- `dotnet build CodexSwitch/CodexSwitch.csproj`

### Agent C22: Settings Segmented Control Migration

Status: completed for active Settings segmented controls. Remaining app migration work still includes old buttons, icon buttons, sections, dialogs, and other legacy `ui:*` controls.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNavigationPrimitives.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitch/Views/Pages/SettingsPage.axaml`
- `CodexSwitch.Tests/AppViewMigrationTests.cs`

Evidence gathered:

- Old `CsSegmentedControl` used a manual timer-driven pill animation and `CsSegmentedButton:selected` pseudo-class.
- New `CodexSegmentedControl` owns a tokenized moving indicator with width, height, and margin transitions plus per-segment hover and pressed transforms.
- Settings segmented buttons are controlled by view-model commands and `IsSelected` bindings, so local sibling selection should not override binding-owned state.

Implementation targets:

- Keep uncontrolled segmented buttons selectable by sibling click. Status: preserved.
- Skip local sibling selection when a segmented button has a `Command`, letting controlled app surfaces update `IsSelected` through the VM. Status: completed.
- Replace Settings tab, theme, and outbound proxy segmented controls with `CodexSegmentedControl`/`CodexSegmentedButton`. Status: completed.
- Guard active views from regressing to old segmented controls. Status: completed in `AppViewMigrationTests`.

Verification:

- `dotnet test CodexSwitchUI/tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --filter FullyQualifiedName~ControlStateTests`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --filter FullyQualifiedName~AppViewMigrationTests`
- `dotnet build CodexSwitch/CodexSwitch.csproj`

### Agent C23: Settings Section Migration

Status: completed for active `CsSection` removal. Remaining app migration work still includes old buttons, icon buttons, dialogs, text primitives, and other legacy `ui:*` controls.

Scope:

- `CodexSwitch/Views/Pages/SettingsPage.axaml`
- `CodexSwitch.Tests/AppViewMigrationTests.cs`

Evidence gathered:

- Old `CsSection` was a card-like local `ContentControl` with `Title`, `Description`, and `Action`.
- New `CodexSection` is the component-library slot primitive used by other active pages and owns `Title`, `Description`, `Actions`, content slot state, and tokenized opacity motion.
- Settings sections do not use the old singular `Action` property, so migration can preserve existing bindings and child layout.

Implementation targets:

- Replace all Settings `<ui:CsSection>` containers with `<cui:CodexSection>`. Status: completed.
- Guard active views from regressing to old `CsSection`. Status: completed in `AppViewMigrationTests`.

Verification:

- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --filter FullyQualifiedName~AppViewMigrationTests`
- `dotnet build CodexSwitch/CodexSwitch.csproj`

### Agent A15: Docs Anatomy Case Expansion

Status: in progress for deeper per-component examples. This slice keeps the existing independent AXAML sample model and strengthens the inline `Show code` workflow under each rendered case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ButtonAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/FieldAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/ToastAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/TabsAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DialogAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/PopoverAnatomy.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- Docs already render each `DocsExampleCase` through `BuildInlineExample`, with a hidden `DocsCodeBlock` and a per-case `Show code`/`Hide code` toggle directly below the preview.
- The next useful gap is not another generic page shell; it is component anatomy cases that show slot composition, action rows, loading labels, close controls, selected content, and manual dismissal states.

Implementation targets:

- Add independent AXAML anatomy examples for Button, Field, Toast, Tabs, Dialog, and Popover. Status: completed.
- Register each anatomy case as an additional `DocsExampleCase`, so its code expands below the current rendered component. Status: completed.
- Extend static and rendered docs tests so these examples cannot disappear silently. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`

### Agent C50: InputOtp Core Component

Status: completed for this missing-core Forms pass. This slice adds `CodexInputOtp` as the Avalonia counterpart to shadcn Input OTP, including root/group/slot/separator composition, paste-style multi-character entry, pattern filtering, active slot state, invalid state, disabled behavior, and keyboard navigation.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexInputOtp.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/InputOtp.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/InputOtp.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/InputOtpStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/InputOtpComposition.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/InputOtpInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn Input OTP exposes root, group, slot, and separator composition, plus examples for copy/paste, pattern filtering, disabled, controlled value, and invalid state.
- Current CodexSwitchUI Forms coverage had TextBox, Textarea, InputGroup, Select, NativeSelect, Checkbox, Radio, Switch, Toggle, and Slider, but no one-time-password slot component.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `CodexInputOtp`, `CodexInputOtpGroup`, `CodexInputOtpSlot`, and `CodexInputOtpSeparator` with Text, MaxLength, Pattern, ActiveIndex, Intent, Size, IsInvalid, complete, active, has-character, and grouped slot classes. Status: completed.
- Implement text entry, paste-style multi-character insertion, Backspace/Delete, Arrow/Home/End navigation, slot click focus, disabled guards, and pattern filtering. Status: completed.
- Add `InputOtp.axaml` with Codex-owned root, group, slot, separator templates, focus adorner suppression, active slot focus ring, invalid/intent styling, grouped corners, disabled opacity, and tokenized transitions. Status: completed.
- Add Docs `forms.input-otp` independent page plus default, states, composition, and interaction AXAML samples; every case renders the component first and exposes its own inline `Show code` toggle. Status: completed.
- Extend static, behavior, docs-layout, rendered lifecycle, and visual fingerprint tests so InputOtp cannot regress out of style registration, keyboard/paste behavior, Docs registration, inline code expansion, or cross-theme screenshots. Status: completed.
- Update docs-site Forms pages in English and Chinese to include Input OTP coverage and review criteria. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C155: AvatarGroup Independent Docs Cases

Status: completed for this Docs coverage pass. This slice promotes `CodexAvatarGroup` from an Avatar anatomy sub-example into its own Feedback Docs page, with local AXAML examples that render the component first and expose their own `Show code` / `Hide code` block directly under each case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/AvatarGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/AvatarGroupStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/AvatarGroupAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/AvatarGroupInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/feedback.mdx`
- `docs-site/content/docs/zh/ui-system/feedback.mdx`

Evidence gathered:

- Docs already centralize the user-requested code reveal contract in `BuildInlineExample`: rendered preview, local `Show code` / `Hide code` button, then `DocsCodeBlock` loaded from that exact sample path.
- The registered Docs catalog had broad page and AXAML coverage, but `CodexAvatarGroup` was still documented only inside `feedback.avatar`, despite being a top-level control in structure tests.
- `CodexAvatarGroup` owns stacked/inline layout, overlap, inherited child size, hidden child filtering, disabled opacity, and `CodexAvatarGroupCount` overflow behavior, so it deserves independent states/anatomy/interaction examples.

Implementation targets:

- Add `feedback.avatar-group` under Feedback with behavior notes, state matrix, and event matrix entries. Status: completed.
- Add Default, States, Anatomy, and Interaction AXAML samples for stacked groups, inline groups, compact/large density, hidden members, disabled state, inherited child sizing, status dots, overflow counts, and host composition. Status: completed.
- Add matching C# preview builders so the Docs app renders the examples before each inline source toggle. Status: completed.
- Extend rendered Docs coverage and visual fingerprints so the new page is exercised across light, dark, and custom themes with expanded code blocks. Status: completed.
- Update docs-site Feedback summaries in English and Chinese to call out AvatarGroup as independent coverage. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`

### Agent C156: Tabs ValueChanged Source Metadata

Status: completed for this Navigation Tabs event-parity slice. This pass keeps the existing `SelectedValue` / `ValueChanged` contract while adding the missing source metadata that lets hosts distinguish primary pointer release, keyboard activation/roving, and programmatic value changes like the Web `onValueChange` path.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexTabs.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/TabsInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- Radix/shadcn Tabs expose a value-driven root with `onValueChange`, automatic/manual activation, loop boundaries, and orientation-aware roving triggers.
- Local `CodexTabsValueChangedEventArgs` already carried old/new item, index, and value metadata, but did not identify whether the selection came from pointer, keyboard, or a host-owned value change.
- Nearby Codex selection controls such as SideNav and SegmentedControl already expose source-aware value-change payloads, so Tabs needed the same event-shape parity.
- Local Docs already render every example before its inline `Show code` / `Hide code` block, so this slice only had to refresh the Tabs interaction example and behavior matrices.

Implementation targets:

- Add `CodexTabsValueChangeSource` with `Programmatic`, `Pointer`, and `Keyboard` values. Status: completed.
- Extend `CodexTabsValueChangedEventArgs` with `Source` while preserving old/new item, index, and value metadata. Status: completed.
- Route roving automatic selection, Enter/Space manual activation, primary pointer release, and `SelectedValue` application through source-aware selection helpers. Status: completed.
- Reject right and middle pointer releases for tab activation and keep disabled tabs suppressed. Status: completed.
- Refresh Tabs Docs notes, state/event matrices, live interaction status, AXAML sample copy, and docs-site Navigation summaries to expose source-aware `ValueChanged`. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~TabsKeyboardSelectionMirrorsWebRovingTriggers|FullyQualifiedName~TabsValueChangedPublishesSourceMetadataAndPrimaryPointerRelease|FullyQualifiedName~TabsValueChangedCarriesWebStyleSourceMetadata|FullyQualifiedName~DocsPagesRenderStateAndEventMatricesForWebParityContracts"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C157: ToggleGroup ValueChanged Source Metadata

Status: completed for this Forms Toggle Group event-parity slice. This pass keeps the standalone Toggle behavior intact while moving `CodexToggleGroup` closer to the Radix/shadcn Toggle Group contract: root-owned single/multiple values now raise `ValueChanged` with source metadata for primary pointer release, keyboard activation, and programmatic selection changes.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexToggle.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ToggleGroupInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- Existing plan evidence for Agent C45 records the Web Toggle Group surface: single/multiple `value`, `onValueChange`, disabled items, roving focus, orientation, loop, item values, and Arrow/Home/End keyboard navigation.
- Nearby Codex selection components now expose source-aware value events, including Tabs, RadioGroup, SideNav, SegmentedControl, Carousel, and Pagination, so Toggle Group was the next form selection surface with an event-source gap.
- Local `CodexToggleGroupValueChangedEventArgs` previously only exposed old/new scalar and array values, making pointer, keyboard, and programmatic paths indistinguishable in Docs and host tests.
- Local Docs already render the Toggle Group examples before the matching inline `Show code` / `Hide code` AXAML block, so this slice refreshed the existing interaction case rather than changing the Docs shell.

Implementation targets:

- Add `CodexToggleGroupValueChangeSource` with `Programmatic`, `Pointer`, and `Keyboard` values. Status: completed.
- Extend `CodexToggleGroupValueChangedEventArgs` with `Source` while preserving the existing old/new value and value-array constructor shape. Status: completed.
- Route item Enter/Space activation through source=Keyboard, primary pointer release through source=Pointer, and `SelectedValue` / `SelectedValues` application through source=Programmatic. Status: completed.
- Suppress right and middle pointer releases for Toggle Group item activation while keeping disabled item and disabled group guards. Status: completed.
- Preserve old/new value metadata for programmatic property changes by snapshotting the previous selected value/value array before applying external selection. Status: completed.
- Refresh Toggle Group Docs notes, state/event matrices, interaction preview status text, AXAML sample copy, and docs-site Forms summaries to expose source-aware `ValueChanged`. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ToggleAndToggleGroupMirrorWebPressedAndSelectionState|FullyQualifiedName~ToggleGroupValueChangedPublishesSourceMetadataAndPrimaryPointerRelease|FullyQualifiedName~ChoiceToggleAndRangeControlsOwnPartsEventsAndNativeChrome|FullyQualifiedName~DocsPagesRenderStateAndEventMatricesForWebParityContracts"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C158: Select And NativeSelect Source-Aware Value Events

Status: completed for this Forms select event-parity slice. This pass keeps the existing popup/open behavior intact while extending both `CodexSelect` and `CodexNativeSelect` so value changes carry source metadata for programmatic, pointer, and keyboard selection paths.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSelect.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNativeSelect.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SelectInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/NativeSelectInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn/Radix Select follows value/open event contracts; local `CodexSelect` and `CodexNativeSelect` already exposed `ValueChanged` and `OpenChanged`, but the value payload only had old/new item, index, and value metadata.
- Nearby form/navigation selection controls now expose source metadata, including RadioGroup, ToggleGroup, Tabs, SideNav, SegmentedControl, Combobox, Carousel, and Pagination.
- The active goal calls out broken component event mechanisms, so Select and NativeSelect should not collapse pointer, keyboard, and host-driven selection into indistinguishable events.
- Docs already render Select and NativeSelect interaction cases above their matching inline AXAML code reveal, so this slice refreshed those cases and behavior matrices rather than changing the Docs shell.

Implementation targets:

- Add `CodexSelectValueChangeSource` and `CodexNativeSelectValueChangeSource` with `Programmatic`, `Pointer`, and `Keyboard` values. Status: completed.
- Extend `CodexSelectValueChangedEventArgs` and `CodexNativeSelectValueChangedEventArgs` with `Source`, keeping constructor defaults backward compatible. Status: completed.
- Track pointer and keyboard selection intent around native ComboBox selection, and expose internal source-aware `SelectIndex` helpers for deterministic host/test paths. Status: completed.
- Preserve existing `OpenChanged`, `popup-open`, placeholder, has-selection, option value, and optgroup behavior. Status: completed.
- Refresh Select/NativeSelect Docs notes, state/event matrices, interaction preview status text, AXAML sample copy, and docs-site Forms summaries to expose source-aware value changes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~SelectRaisesWebValueAndOpenChangeEvents|FullyQualifiedName~NativeSelectRaisesWebValueAndOpenChangeEvents|FullyQualifiedName~SelectStyleOwnsPopupItemsAndOpeningMotion|FullyQualifiedName~NativeSelectStyleOwnsOptionsGroupsInvalidAndOpeningMotion|FullyQualifiedName~DocsPagesRenderStateAndEventMatricesForWebParityContracts"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C111: Data Display Surface Anatomy Docs And Chrome Guard

Status: completed for this Docs coverage slice. This pass raises the remaining Data Display surface/media/viewport pages from three examples to four by adding independent anatomy cases, while preserving the inline `Show code` / `Hide code` AXAML reveal under each rendered component case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/CardAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/MetricAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ImageIconAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ProviderCardAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ScrollAreaAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ApplicationShell.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- The selected pages were the remaining under-covered Data Display surfaces with three cases: Card, Metric, Image icon, Provider card, and Scroll area.
- The existing Docs `BuildInlineExample` path already renders the preview first and mounts a local `DocsCodeBlock` under that exact example after `Show code`, so each new case only needed a standalone AXAML file plus registration.
- ProviderCard, StatCard, Metric, ImageIcon, and ScrollArea are native-derived or native-composing surfaces where explicit template parts and scoped selectors guard against default Avalonia/Fluent style leakage.

Implementation targets:

- Add `CardAnatomy`, `MetricAnatomy`, `ImageIconAnatomy`, `ProviderCardAnatomy`, and `ScrollAreaAnatomy` standalone AXAML samples and preview builders. Status: completed.
- Register each anatomy sample under its component page so the rendered example gets its own local code reveal. Status: completed.
- Name StatCard, Metric, and ProviderCard template parts more completely, and add ProviderCard focus-visible and disabled style hooks. Status: completed.
- Add Docs layout tests for the new samples and a structure guard covering Data Display slots, image lifecycle events, provider-card selection/dragging classes, scroll-area viewport parts, motion tokens, and native-style leakage. Status: completed.
- Refresh reduced-motion visual fingerprints after the new rendered cases were added. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~NavigationDataComponentTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C112: Data Display Chart And Pager Anatomy Docs

Status: completed for this Docs coverage and native chrome guard slice. This pass completes the remaining Data Display under-covered pages by adding independent anatomy cases for pinned table, pagination, ranked bar chart, usage pie chart, and usage trend chart, while keeping each rendered example paired with its local `Show code` / `Hide code` AXAML reveal.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/PinnedTableAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/PaginationAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/RankedBarChartAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/UsagePieChartAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/UsageTrendChartAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Table.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- The selected pages were the remaining Data Display pages with only three cases: Pinned table, Pagination, Ranked bar chart, Usage pie chart, and Usage trend chart.
- Pinned table and plain table templates contained raw `ScrollViewer` usage, so this slice added scoped table/pinned-table `ScrollViewer`, `ScrollBar`, `Track`, and `Thumb` templates instead of letting platform chrome leak through.
- Pagination already uses Codex-owned buttons and page-button classes; charts expose pointer/active state and animation properties in control code, so the missing work was docs anatomy coverage plus stronger structure guards.

Implementation targets:

- Add `PinnedTableAnatomy`, `PaginationAnatomy`, `RankedBarChartAnatomy`, `UsagePieChartAnatomy`, and `UsageTrendChartAnatomy` standalone AXAML samples and preview builders. Status: completed.
- Register each anatomy sample under its component page so every case can reveal its own source below the rendered component. Status: completed.
- Name additional pinned-table template regions and add scoped Codex table scroll viewer/scrollbar templates. Status: completed.
- Extend Docs layout tests and component structure guards for pinned-table scroll synchronization, pagination page events and keyboard navigation, chart active states, animation tokens, and native scroll chrome leakage. Status: completed.
- Refresh reduced-motion Docs visual fingerprints after the new cases and scroll chrome changes. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~NavigationDataComponentTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`

### Agent C113: Final Low-Coverage Docs Case Expansion

Status: completed for this Docs coverage slice. This pass clears the last low-coverage Docs pages so every registered page now has at least four rendered examples, with each example keeping its local `Show code` / `Hide code` AXAML reveal directly under the component case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overview/GettingStartedAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overview/GettingStartedWorkflow.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overview/GettingStartedSource.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/ApplicationShellAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/SidebarPrimitivesAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/SectionAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Primitives/TypographyAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Primitives/FocusRingAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Primitives/DirectionAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Primitives/OverlayAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Tokens/MotionAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- The remaining low-coverage pages were `overview.getting-started`, `layout.application-shell`, `layout.sidebar-primitives`, `layout.section`, `primitives.typography`, `primitives.focus-ring`, `primitives.direction`, `primitives.overlay`, and `tokens.motion`.
- `BuildInlineExample` already places a local toggle and `DocsCodeBlock` below each rendered case, so the missing work was standalone AXAML files, preview builders, and page registration.
- Docs visual tests treat `Show code` / `Hide code` as reserved toggle text, so in-preview source affordances use distinct labels such as `Source` and `Inspect source`.

Implementation targets:

- Add three Overview workflow/source/anatomy cases and one anatomy case for each remaining Layout, Primitive, and Motion-token page. Status: completed.
- Register every new case in `ExampleCasesFor` with matching preview builders, keeping the rendered component first and code reveal local to the case. Status: completed.
- Add `EveryDocsPageExposesAtLeastFourInlineCodeExamples` so future edits cannot regress page-level case coverage below four examples. Status: completed.
- Refresh reduced-motion Docs visual fingerprints after the new examples were added. Status: completed.
- Re-run the coverage audit and confirm `TOTAL_LT4 0`. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `python3` coverage audit for registered Docs example counts: `TOTAL_LT4 0`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~NavigationDataComponentTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`

### Agent C114: Overlay Primary Trigger Event Parity

Status: completed for this overlay trigger event parity slice. This pass aligns Dialog, Alert Dialog, Command Dialog, Sheet, Drawer, and Popover trigger activation with the Web/button-like contract by allowing only primary pointer release to toggle the open state, while keeping Enter/Space keyboard activation unchanged.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexPopover.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- `CodexDialog` and `CodexPopover` previously toggled from `ContentPresenter.PointerReleased` without checking which pointer button was released.
- `CodexSheet`, `CodexDrawer`, `CodexAlertDialog`, and `CodexCommandDialog` inherit the `CodexDialog` trigger path, so one guarded dialog path covers that whole overlay family.
- Web trigger semantics map to primary pointer activation plus keyboard Enter/Space; secondary pointer releases should not open or close these overlays.
- Docs event matrices were still describing a generic trigger click, so the visible contract needed to say primary trigger activation explicitly.

Implementation targets:

- Add `TryHandleTriggerPointerRelease(PointerUpdateKind updateKind)` to `CodexDialog` and `CodexPopover`, returning true only for `PointerUpdateKind.LeftButtonReleased` when the control can toggle. Status: completed.
- Update trigger pointer handlers to read `Properties.PointerUpdateKind` from the current pointer point before toggling. Status: completed.
- Add overlay behavior coverage proving right and middle button release do not open Dialog, Alert Dialog, Command Dialog, Sheet, Drawer, or Popover, while primary release still opens and disabled controls ignore activation. Status: completed.
- Add a structure guard so the primary-pointer-only trigger path cannot be accidentally collapsed back into an unqualified pointer release. Status: completed.
- Update the Docs overlay event matrix labels from generic trigger click to primary trigger activation. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 68 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 236 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C115: Navigation Disclosure Primary Trigger Parity

Status: completed for this Navigation event parity slice. This pass moves Collapsible and Accordion trigger activation from pointer-press toggling to Web-style primary pointer release, while preserving the existing measured-height open/close animation and keyboard trigger contract.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCollapsible.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexAccordion.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- `CodexCollapsible` was attaching `InputElement.PointerPressedEvent` to the trigger layout and toggling as soon as the primary button was pressed.
- shadcn/Radix-style disclosure and accordion triggers are button-like surfaces; activation should resolve from primary click/release plus Enter/Space, not from press-down alone.
- `CodexAccordionItem` inherits the Collapsible trigger template and overrides toggle behavior, so it needed an explicit pointer-release path that reports `CodexAccordionValueChangeSource.Trigger` while guarding disabled item/root states.
- Docs described the Collapsible trigger as a generic click path and Accordion did not call out primary pointer release, leaving the visible contract less precise than the component behavior.

Implementation targets:

- Replace Collapsible trigger-layout `PointerPressedEvent` wiring with `PointerReleasedEvent` and read `Properties.PointerUpdateKind` before toggling. Status: completed.
- Add `TryHandleTriggerPointerRelease(PointerUpdateKind updateKind)` to Collapsible, accepting only `PointerUpdateKind.LeftButtonReleased` and returning false when disabled. Status: completed.
- Override Accordion item pointer release handling so primary release toggles through the Accordion root with source metadata, while disabled items or disabled roots suppress activation. Status: completed.
- Tighten Collapsible keyboard handling so disabled triggers no longer report successful Enter/Space activation. Status: completed.
- Update Navigation behavior tests and structure guards for primary-only pointer release, secondary-button suppression, disabled guards, and release-event wiring. Status: completed.
- Update Docs page notes and event matrices to describe primary trigger activation for Accordion and Collapsible. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 116 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 237 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C116: Dropdown Keyboard Trigger Parity

Status: completed for this popup-trigger keyboard parity slice. This pass adds the missing ArrowDown trigger-open path for Dropdown Button and Split Button menu trigger surfaces, matching the Web DropdownMenu-style keyboard contract while preserving existing primary click, Enter/Space button activation, close-on-select, Escape dismissal, and focus-return behavior.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDropdownButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSplitButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- Dropdown Button and Split Button already rely on `CodexButton.Click` for primary pointer release plus Enter/Space activation, but neither trigger had an explicit ArrowDown key path to open the popup.
- Radix/shadcn dropdown-style triggers support keyboard opening from ArrowDown, so a focused trigger should open the menu without requiring a click-style activation.
- Split Button separates the primary action and menu trigger; ArrowDown should open only the menu trigger popup and must not run the primary action command.
- Docs described only generic trigger click/toggle behavior, leaving the keyboard-open contract unclear for both the Forms split-button page and Navigation dropdown page.

Implementation targets:

- Add trigger `KeyDown` wiring to `CodexDropdownButton` so `Key.Down` opens the popup through `TryHandleTriggerKey`. Status: completed.
- Add menu-trigger `KeyDown` wiring to `CodexSplitButton` so `Key.Down` opens the dropdown through `TryHandleMenuTriggerKey` without invoking the primary action. Status: completed.
- Preserve existing `CodexButton.Click` activation for primary click and Enter/Space to avoid duplicate toggles. Status: completed.
- Add behavior tests covering ArrowDown open, non-ArrowDown no-op for the explicit trigger-key helper, loading suppression, missing-content suppression, and primary-action separation. Status: completed.
- Add structure guards for trigger key handlers and update Docs page notes/event matrices for ArrowDown keyboard opening. Status: completed.
- Refresh reduced-motion Docs visual fingerprints after the Navigation dropdown event matrix text changed. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 141 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 237 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C117: Menubar Primary Trigger Event Parity

Status: completed for this Menubar trigger parity slice. This pass aligns top-level Menubar trigger activation with the same Web-style primary pointer release rule now used by Dialog, Popover, Collapsible, Accordion, Dropdown, and Split Button trigger surfaces.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexMenubar.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- `CodexMenubarItem.OnPointerReleased` toggled top-level submenu state for any pointer release as long as the item was top-level and had submenu content.
- Web/Radix menubar triggers are button-like menu triggers; secondary pointer releases should not open or close top-level menus.
- Menubar keyboard behavior was already explicit through `TryHandleTopLevelNavigationKey`, so the missing parity gap was the pointer release filter.
- Docs still described a generic top-level trigger click, while the component contract now needs to name primary pointer release consistently with the other trigger families.

Implementation targets:

- Add `TryHandleTopLevelPointerRelease(CodexMenubarItem item, PointerUpdateKind updateKind)` to `CodexMenubar`, accepting only `PointerUpdateKind.LeftButtonReleased`. Status: completed.
- Route top-level `CodexMenubarItem.OnPointerReleased` through the new helper using `Properties.PointerUpdateKind`, leaving right and middle releases as no-op for menu open/close state. Status: completed.
- Preserve existing `ToggleMenu`, loading guards, disabled guards, pointer-enter menu switching, and keyboard open/navigation paths. Status: completed.
- Add behavior tests proving right/middle release does not open or close a top-level menu, left release toggles, and loading suppresses left-release open. Status: completed.
- Add a structure guard so the top-level pointer path cannot regress to direct `ToggleMenu` without checking `PointerUpdateKind`. Status: completed.
- Update Docs event matrix text for Menubar primary trigger activation. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 117 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 238 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C70: Command Filtering, Keyboard Parity, And Docs Cases

Status: completed for this Navigation and Overlay command pass. This slice upgrades `CodexCommand` toward the shadcn/cmdk contract and adds missing Docs cases while preserving the existing per-example inline `Show code` / `Hide code` AXAML workflow.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCommand.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCommandDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Command.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/CommandDialog.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/CommandFiltering.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/CommandScrollable.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/CommandDialogAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- shadcn Command documents `Command`, `CommandInput`, `CommandList`, `CommandEmpty`, `CommandGroup`, `CommandItem`, `CommandShortcut`, and `CommandSeparator`, plus grouped, shortcut, scrollable, combobox, and dialog compositions.
- Local Command previously rendered input/groups/items/loading/empty, but shortcut was only a string slot, separator borrowed native `Separator`, the list was not scroll-owned, and root keyboard/search/selected event behavior was missing.
- Local Docs already render each example first and use `BuildInlineExample` to put the current sample AXAML code behind a local button under that exact case, so the missing work is independent AXAML coverage plus page registration.

Implementation targets:

- Add `SearchText`, `ShouldFilter`, `LoopNavigation`, `SelectedItem`, `ItemSelected`, `TryHandleNavigationKey`, `TrySelectActiveItem`, item `Value` and `Keywords`, pointer-hover active state, and loading-safe selection suppression to `CodexCommand`. Status: completed.
- Add `CodexCommandShortcut` and `CodexCommandSeparator` with `AlwaysRender`, tokenized styles, filtered-out classes, and scrollable `CodexCommandList` template support. Status: completed.
- Forward Command search/filter/loop behavior through `CodexCommandDialog`, and expose the same item-selected event path before close-on-select dismissal. Status: completed.
- Add Navigation examples for filtering and scrollable command lists, add Overlay command dialog anatomy, and keep every case wired through `BuildInlineExample`. Status: completed.
- Update state/event matrices and docs-site EN/ZH pages to mention search filtering, separators, shortcut slots, scrollable lists, loop navigation, and selected events. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests"`: 140 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 204 passed.
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`: 184 passed.
- `npm run lint` in `docs-site`: passed.
- `npm run build` in `docs-site`: passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C71: NavigationMenu Anatomy, Value, And Link Activation Parity

Status: completed for this NavigationMenu parity pass. This slice closes the missing shadcn/Radix anatomy example and extends the Avalonia control contract with value-driven active state, active-item change events, and link activation paths while keeping every Docs example rendered before its local inline AXAML code toggle.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNavigationMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/NavigationMenuAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- shadcn Navigation Menu documents the public composition as Root/List/Item/Trigger/Content/Link/Indicator, including a top-level Link item.
- Radix Navigation Menu exposes controlled `value` / `onValueChange`, item `value`, link `active` / `onSelect`, an optional indicator, a viewport, orientation, keyboard navigation, and motion state.
- Local Docs already place `BuildInlineExample` under each rendered example, so the Docs gap was an independent `NavigationMenuAnatomy.axaml` sample plus page registration and preview builder.

Implementation targets:

- Add `ActiveValue`, `ActiveItemChanged`, value resolution, controlled-style active item syncing, Escape close, and horizontal/vertical roving navigation helpers on `CodexNavigationMenu`. Status: completed.
- Add `Value`, `Command`, `CommandParameter`, `Activated`, and top-level link activation to `CodexNavigationMenuItem`. Status: completed.
- Add `Command`, `CommandParameter`, `Activated`, pointer/key activation, focus-visible, and activation classes to `CodexNavigationMenuLink`. Status: completed.
- Add `Navigation/NavigationMenuAnatomy.axaml` showing trigger/content/link anatomy, active value, active route, viewport sizing, and top-level link composition, then register it under the Navigation menu Docs page. Status: completed.
- Update tests and docs-site EN/ZH pages to cover active value, active item changes, top-level link commands, content link activation, and anatomy coverage. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests"`: 102 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 204 passed.
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`: 184 passed.
- `npm run lint` in `docs-site`: passed.
- `npm run build` in `docs-site`: passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C72: Feedback Anatomy Cases And Avatar Group Primitive

Status: completed for this Feedback anatomy and missing primitive pass. This slice follows current shadcn Alert, Badge, and Avatar documentation by adding standalone anatomy examples and the missing AvatarGroup / AvatarGroupCount composition primitive, while preserving the Docs contract that each rendered case exposes its own inline AXAML source toggle.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexAvatarGroup.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Avatar.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/AlertAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/BadgeAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/AvatarAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `docs-site/content/docs/en/ui-system/feedback.mdx`
- `docs-site/content/docs/zh/ui-system/feedback.mdx`

Evidence gathered:

- shadcn Alert documents Icon, AlertTitle, AlertDescription, and AlertAction composition, including basic, destructive, action, and custom-color cases.
- shadcn Badge documents default/secondary/destructive/outline/ghost/link variants, inline icon content, spinner content, link rendering, and custom colors.
- shadcn Avatar documents AvatarImage, AvatarFallback, AvatarBadge, AvatarGroup, and AvatarGroupCount composition, including grouped overlap, overflow count, sizes, and dropdown trigger examples.
- Local Docs already render every `DocsExampleCase` before its `BuildInlineExample` source toggle, so the missing Docs work was independent anatomy AXAML files plus page registration and rendered verification.

Implementation targets:

- Add `CodexAvatarGroup` as an overlapping avatar panel with `Size`, `Overlap`, `IsStacked`, `ItemCount`, group item classes, and size propagation to avatar/count children. Status: completed.
- Add `CodexAvatarGroupCount` with `Count`, generated `+N` content, size classes, count/empty classes, and Avatar style selectors. Status: completed.
- Add Feedback anatomy AXAML samples for Alert slot composition, Badge variants/icon/spinner/link/status compositions, and Avatar fallback/status/group/count compositions. Status: completed.
- Register anatomy examples under `feedback.alert`, `feedback.badge`, and `feedback.avatar`, each using the existing inline code expansion path. Status: completed.
- Update state/style/docs tests and docs-site EN/ZH pages to cover the new primitive and anatomy cases. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests"`: 71 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 204 passed.
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`: 184 passed.
- `npm run lint` in `docs-site`: passed.
- `npm run build` in `docs-site`: passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C73: Badge Link Activation Event Parity

Status: completed for this Feedback interaction pass. This slice tightens `CodexBadge` around the current shadcn Badge link/asChild scenarios by adding an optional activation contract for link-like badges while preserving the default static label behavior.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexBadge.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Badge.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/BadgeInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `docs-site/content/docs/en/ui-system/feedback.mdx`
- `docs-site/content/docs/zh/ui-system/feedback.mdx`

Evidence gathered:

- shadcn Badge documents variants plus link/asChild rendering, inline icon content, spinner content, and badge-as-link usage.
- Local Badge already owned variants, sizes, status dots, and anatomy Docs cases, but it did not expose an event path for Web-like link badge activation.
- Local Docs already renders every case before `BuildInlineExample`, so the interaction gap could be shown by extending the existing `Feedback/BadgeInteraction.axaml` case instead of adding page-local prose.

Implementation targets:

- Add optional `IsInteractive`, `Command`, `CommandParameter`, `CanActivate`, `Activated`, and `TryActivate` to `CodexBadge`. Status: completed.
- Add pointer release activation, Enter/Space keyboard activation, command `CanExecute` suppression, focus-visible tracking, and command change resync. Status: completed.
- Add `interactive`, `can-activate`, and `command-blocked` classes plus focus-visible/link-hover styles while leaving default badges non-focusable. Status: completed.
- Extend Badge interaction Docs with link badge activation and disabled/blocked examples. Status: completed.
- Update Feedback tests and docs-site EN/ZH notes to cover link-like badge command activation, focus-visible, and keyboard activation. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests"`: 72 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 205 passed.
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`: 184 passed.
- `npm run lint` in `docs-site`: passed.
- `npm run build` in `docs-site`: passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C63: Direction Primitive And Docs Cases

Status: completed for this missing shadcn primitive pass. This slice adds a `CodexDirection` provider so Avalonia content can mirror the Web DirectionProvider contract: host state switches between `ltr` and `rtl`, child layout inherits the direction, and nested islands can override the parent direction for code/API content.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDirection.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Direction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Primitives/Direction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Primitives/DirectionStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Primitives/DirectionInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `docs-site/content/docs/en/ui-system/index.mdx`
- `docs-site/content/docs/zh/ui-system/index.mdx`

Evidence gathered:

- The current shadcn component index lists `Direction` as a component alongside the rest of the covered families.
- shadcn Direction is a provider that sets application text direction for `ltr` / `rtl`, and the RTL guide calls out first-class right-to-left support with DirectionProvider.
- Avalonia has an inheritable `FlowDirection`, so the desktop equivalent can be a transparent content wrapper rather than a visual card component.
- Existing Docs architecture already renders each example above its own local `Show code` / `Hide code` AXAML block through `BuildInlineExample`.

Implementation targets:

- Add `CodexDirection` with `Direction`, `IsRightToLeft`, `DirectionChanged`, `direction-ltr`, `direction-rtl`, and FlowDirection synchronization. Status: completed.
- Add a transparent tokenized `Direction.axaml` provider style and register it in `ComponentStyles.axaml`. Status: completed.
- Add Docs `primitives.direction` page with default, state, and interaction AXAML samples that show LTR/RTL forms, mirrored actions, nested overrides, disabled state, and runtime direction changes. Status: completed.
- Extend component structure/state guards, Docs static registration, rendered lifecycle coverage, all-page visual fingerprints, and docs-site EN/ZH overview notes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`

### Agent C74: Checkbox Checked-State Event, Anatomy Docs, And Selectable Code Blocks

Status: completed for this Forms parity slice. This pass tightens Checkbox against the Web `onCheckedChange` contract, adds the missing anatomy/composition Docs case, and makes the Docs code viewer text selectable by mouse while keeping the existing copy button.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCheckBox.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Checkbox.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Controls/DocsCodeBlock.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/CheckboxAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn Checkbox exposes checked, unchecked, indeterminate, disabled, label/field composition, and `onCheckedChange` as the core checked-state event path.
- Local Checkbox already owned glyph, focus-visible, pressed, intent, size, and indeterminate visuals, but it did not expose a dedicated checked-state event or explicit Web-style state classes.
- Local Docs already renders every example before the inline `Show code` / `Hide code` toggle, but the code viewer used non-selectable `TextBlock` rows, so mouse text selection was incomplete.

Implementation targets:

- Add `CheckedStateChanged` with old/new `bool?` values to mirror Web `onCheckedChange`, including indeterminate transitions. Status: completed.
- Add `state-checked`, `state-unchecked`, and `state-indeterminate` classes and wire Checkbox selectors to those state classes alongside Avalonia pseudoclasses. Status: completed.
- Add `Forms/CheckboxAnatomy.axaml` covering root/label targeting, checked/unchecked/mixed indicator states, FieldSet grouping, validation, required marker, disabled, and intent composition. Status: completed.
- Register the Checkbox anatomy case in Docs examples, behavior state/event matrices, layout guards, and rendered lifecycle tests. Status: completed.
- Replace per-line non-selectable code text with one `SelectableTextBlock` plus line-number rail so users can mouse-select source text while retaining the copy button. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests"`: 81 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 206 passed.
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`: 184 passed.
- `npm run lint` in `docs-site`: passed.
- `npm run build` in `docs-site`: passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C78: Ranked Bar Chart Active Row Event Contract

Status: completed for this event-triggering parity slice. This pass fixes the next chart-family event gap: `CodexRankedBarChart` now exposes active-row state and event metadata instead of rendering static rows with no observable hover path.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexRankedBarChart.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/RankedBarChart.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/RankedBarChartInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- `CodexBarChart`, `CodexLineChart`, and `CodexUsagePieChart` now expose public active-item or active-point event paths, while `CodexRankedBarChart` still had no active row state.
- The active goal explicitly calls out broken event-triggering mechanisms, so ranked rows need old/new active row metadata and style hooks rather than remaining visual-only.
- The shared interaction contract in this plan requires active state to be property-driven and testable.

Implementation targets:

- Add `ActiveIndex`, `ActiveItem`, and `ActiveItemChanged` to `CodexRankedBarChart`. Status: completed.
- Route pointer row hover and pointer exit through the public active index path. Status: completed.
- Draw active-row emphasis and expose `ranked-bar-chart`, `empty`, and `has-active-row` classes. Status: completed.
- Add style hooks for active row, pointer hover, and empty state. Status: completed.
- Update Docs behavior/event matrices, interaction copy, and docs-site text to include `ActiveItemChanged`. Status: completed.
- Add tests proving class sync, active item lookup, and old/new index event delivery. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`: passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~MotionRenderedLifecycleTests"`: 116 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 208 passed.
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`: 184 passed.
- `npm run lint` in `docs-site`: passed.
- `npm run build` in `docs-site`: passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C77: Usage Pie Chart Active Slice Event Contract

Status: completed for this event-triggering parity slice. This pass fixes a concrete event-mechanism gap in `CodexUsagePieChart`: pointer hover and controlled active-slice updates now flow through public state and an explicit event instead of staying trapped in a private hovered index.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexUsagePieChart.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/UsagePieChart.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/UsagePieChartInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- `CodexBarChart` and `CodexLineChart` already expose `ActiveItemChanged` / `ActivePointChanged`, but `CodexUsagePieChart` still used a private `_hoveredIndex` for slice hover state.
- The active goal now explicitly calls out broken component event mechanisms, so chart hover needs an externally observable, testable event contract instead of only visual tooltip movement.
- shadcn Chart tooltip examples treat active payload state as part of chart interaction, so the Avalonia chart should surface old/new active slice metadata.

Implementation targets:

- Add `ActiveIndex`, `ActiveItem`, and `ActiveItemChanged` to `CodexUsagePieChart`. Status: completed.
- Route pointer hover, legend hover, tooltip, slice emphasis, and hover clearing through the public active index path. Status: completed.
- Add `usage-pie-chart`, `empty`, and `has-active-slice` state classes plus style hooks for active tooltip border and empty track state. Status: completed.
- Update Docs behavior/event matrices and interaction copy to mention `ActiveItemChanged`; keep each AXAML example rendered before its inline code toggle. Status: completed.
- Add tests proving class sync, active item lookup, and old/new index event delivery. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`: passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~MotionRenderedLifecycleTests"`: 116 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed. First parallel attempt hit a transient PDB file lock while another test build was running; the single rerun passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 208 passed.
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`: 184 passed.
- `npm run lint` in `docs-site`: passed.
- `npm run build` in `docs-site`: passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C76: Native Bar Chart Component, Events, And Docs Cases

Status: completed for this Data Display chart-library and event-contract slice. This pass adds a native Avalonia `CodexBarChart` so the library covers common Web vertical and horizontal bar chart examples with explicit active-item event behavior, not just static ranked bars.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexBarChart.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/BarChart.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/BarChart.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/BarChartStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/BarChartAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/BarChartInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- Current Web chart coverage expects bar charts as a first-class chart body alongside line and pie charts, with helper composition for container, legend, tooltip, and config colors.
- Local coverage after C75 had a generic Line Chart, usage pie, ranked bar, and usage trend, but still lacked a native generic Bar Chart with vertical and horizontal orientation.
- The updated goal calls out broken event-triggering mechanisms, so this slice treats active bar changes as a first-class event contract with tests instead of a purely visual chart pass.

Implementation targets:

- Add `CodexBarChart` with ordered `ItemsSource` items, vertical/horizontal orientation, grid and axis-label toggles, compact density, negative-value zero baseline, empty state, active bar, tooltip interpolation, and tokenized redraw motion. Status: completed.
- Add `ActiveItemChanged` with old/new index and item metadata for Web-style hover or controlled active-bar handling. Status: completed.
- Add `BarChart.axaml` with token resources for bars, active bars, grid, tooltip, slow animation duration, compact, horizontal, no-grid, no-axis-labels, negative-value, active, size, hover, and disabled states. Status: completed.
- Register Bar Chart in theme includes, component/style guards, state tests, motion rendered lifecycle tests, Docs page registry, state/event matrices, representative and multi-case render coverage, and visual fingerprints. Status: completed.
- Add independent Docs AXAML examples for default, states, anatomy, and interaction, each rendered before its inline code toggle. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`: passed.
- `dotnet build src/CodexSwitchUI.Docs/CodexSwitchUI.Docs.csproj -f net10.0 --no-restore`: passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~MotionRenderedLifecycleTests"`: 135 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 208 passed.
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`: 184 passed.
- `npm run lint` in `docs-site`: passed.
- `npm run build` in `docs-site`: passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C75: Native Line Chart Component And Docs Cases

Status: completed for this Data Display chart-library slice. This pass adds a native Avalonia `CodexLineChart` so the library now covers common Web line/area chart examples instead of only chart helpers plus specialized usage charts.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexLineChart.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/LineChart.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/LineChart.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/LineChartStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/LineChartAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/LineChartInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- Current shadcn Chart docs describe a composition helper model built around `ChartContainer`, `ChartTooltip`, and chart engine content, and the current examples include `LineChart accessibilityLayer` as a first-class chart body.
- Local chart coverage already had `CodexChartContainer`, legend, tooltip, ranked bar, usage pie, and ECharts usage trend, but lacked a native generic line/area chart body for dashboard trend cases.
- The updated goal specifically calls for more chart components, detailed transitions, examples, and code block inspection, so Line Chart needs its own page and inline AXAML examples.

Implementation targets:

- Add `CodexLineChart` with ordered `ItemsSource` points, line/area/dot/grid toggles, compact density, empty state, active point, tooltip interpolation, and tokenized redraw motion. Status: completed.
- Add `ActivePointChanged` with old/new index and point metadata for Web-style hover/active-point handling. Status: completed.
- Add `LineChart.axaml` with token resources for line, area, grid, dots, tooltip, slow animation duration, compact, line-only, no-grid, empty, active, size, hover, and disabled states. Status: completed.
- Register Line Chart in theme includes, component/style guards, state tests, motion rendered lifecycle tests, Docs page registry, state/event matrices, representative and multi-case render coverage, and visual fingerprints. Status: completed.
- Add independent Docs AXAML examples for default, states, anatomy, and interaction, each rendered before its inline code toggle. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~MotionRenderedLifecycleTests"`: 134 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 207 passed.
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`: 184 passed.
- `npm run lint` in `docs-site`: passed.
- `npm run build` in `docs-site`: passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C64: Radio Group Form Component And Docs Cases

Status: completed for this Forms parity pass. This slice adds a root-owned `CodexRadioGroup` so the desktop component library mirrors shadcn/Radix Radio Group instead of only styling individual radio buttons.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexRadioGroup.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/RadioGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/RadioGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/RadioGroupStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/RadioGroupAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/RadioGroupInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn Radio Group is a dedicated root plus item component, not just isolated radio buttons.
- Radix Radio Group owns value/defaultValue/onValueChange, name, required, disabled, orientation, loop, roving focus, checked/unchecked item states, Space activation, and Arrow navigation.
- Existing `CodexRadio` already owns the item visual with focus-visible and checked dot motion, so the missing piece was a group root and item value contract.
- Existing Docs architecture already renders each case above its local `Show code` / `Hide code` AXAML block.

Implementation targets:

- Add `CodexRadioGroup` with `SelectedValue`, `RadioGroupName`, `Orientation`, `Intent`, `Size`, `IsLoop`, `IsRovingFocus`, `IsRequired`, `IsLoading`, value-change events, disabled-item skipping, loading suppression, and Arrow/Home/End selection. Status: completed.
- Add `CodexRadioGroupItem` with `Value`, `IsRequired`, `state-checked`, `state-unchecked`, and group-owned Space/Enter activation. Status: completed.
- Add `RadioGroup.axaml` with group root template, horizontal/vertical panels, loading/disabled state, item state selectors, intent/size selectors, and tokenized transitions. Status: completed.
- Add Docs `forms.radio-group` page with default, states, anatomy, and interaction AXAML samples, each rendered before its inline source toggle. Status: completed.
- Extend structure/state/detail tests, Docs static registration, rendered lifecycle coverage, visual fingerprints, and docs-site EN/ZH Forms notes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`

### Agent C198: Popup Align Effective Placement And Default Closed Docs Samples

Status: completed for this overlay/default-behavior parity slice. This pass makes trigger-owned popup components translate Web `side + align` semantics into actual Avalonia popup placement instead of relying on surface-local alignment, and removes accidental default-open behavior from non-state Docs composition examples.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexPopupPlacement.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexPopover.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexHoverCard.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDropdownButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSplitButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Popover.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/HoverCard.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/DropdownButton.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/SplitButton.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/CalendarComposition.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/MenubarComposition.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- Agent audit confirmed Popover, HoverCard, DropdownButton, and SplitButton exposed `Align`, but templates still bound native `Popup.Placement` directly to `Placement`; `align-start/end` only affected local surface alignment.
- Web/Radix side + align maps to Avalonia edge-aligned placement: bottom/top start-end map to left/right edges, and left/right start-end map to top/bottom edges.
- Default-open Docs audit found accidental open composition examples in Calendar and Menubar, while `*Anatomy.axaml` and `*States.axaml` examples intentionally remain open to demonstrate visual states.

Implementation targets:

- Add shared `CodexPopupPlacement.Resolve(...)` for Popover, HoverCard, DropdownButton, and SplitButton align enums. Status: completed.
- Add `EffectivePlacement` to the four trigger-owned popup controls and update templates to bind `Popup.Placement` to it. Status: completed.
- Preserve explicitly edge-aligned `Placement` values when `Align=Center`, and preserve non-side Avalonia placement modes instead of folding them to bottom. Status: completed.
- Convert Calendar composition to a closed-by-default Popover trigger composition, and remove default-open submenu flags from Menubar composition. Status: completed.
- Add tests for the 12 Web side+align mappings across all four controls, template binding to `EffectivePlacement`, and a static guard that non-state Docs AXAML samples do not default disclosure controls open. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~DocsNonStateSamplesDoNotDefaultDisclosureControlsOpen"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C190: Forms Interaction Examples Default Closed

Status: completed for this default-behavior Docs pass. This slice fixes Forms interaction examples that were still starting popup/disclosure components open, so the rendered Docs behavior now matches Web defaults: closed by default, open only through trigger, keyboard, or an explicit demo command.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SelectInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ComboboxInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SplitButtonInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/DatePickerInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/SelectInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/ComboboxInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/SplitButtonInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/DatePickerInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- A read-only local Agent audited `MainWindow.cs` and the Forms companion C# interaction samples for `Combobox`, `NativeSelect`, `DatePicker`, and `SplitButton`; it flagged initial-open usage in Combobox, DatePicker, and SplitButton interaction docs.
- Local inspection also found `Forms/SelectInteraction.axaml` and `BuildSelectInteractionPreview` starting `CodexSelect` with `IsDropDownOpen=true`, so Select was included in the same default-closed cleanup.
- Web-style select, combobox, date picker, and split-button surfaces should enter closed and disclose through trigger, keyboard, or controlled state, not through a docs interaction case's initial object initializer.

Implementation targets:

- Remove initial `IsDropDownOpen=true` / `IsOpen=true` / `isOpen: true` from Forms Select, Combobox, SplitButton, and DatePicker interaction previews. Status: completed.
- Add explicit small demo buttons where needed: Select `Open`, Combobox `Open`, SplitButton `Open`/`Dismiss`, DatePicker `ArrowDown`/`Escape`, and DatePicker range `Open range`/`Finish range`. Status: completed.
- Keep state/anatomy examples free to show open surfaces, while interaction examples now model default closed behavior. Status: completed.
- Add `FormInteractionDocsDoNotStartPopupExamplesOpen` to guard the MainWindow methods, companion C# samples, and standalone AXAML interaction samples against initial-open regressions. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C191: Navigation Interaction Examples Default Closed

Status: completed for this default-behavior Docs pass. This slice extends the default-closed cleanup from Forms into high-risk Navigation interaction examples, so Dropdown, Accordion, and Collapsible examples no longer mount already-open surfaces in the interaction case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/DropdownButtonInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/AccordionInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/CollapsibleInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/DropdownButtonInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/AccordionInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/CollapsibleInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- A read-only local Agent audited only `BuildDropdownInteractionPreview`, `BuildAccordionInteractionPreview`, `BuildCollapsibleInteractionPreview`, and the matching Navigation companion C# files; it flagged initial-open usage in all three interaction families.
- Local AXAML inspection found matching `IsOpen="True"` in `Navigation/DropdownButtonInteraction.axaml`, `Navigation/AccordionInteraction.axaml`, and `Navigation/CollapsibleInteraction.axaml`.
- Web-style dropdown/disclosure/accordion interaction examples should start closed and demonstrate open behavior through a trigger, keyboard path, or explicit demo command; state/anatomy examples can still show open states separately.

Implementation targets:

- Remove initial `IsOpen=true` from DropdownButton, Accordion, and Collapsible interaction previews and companion C# samples. Status: completed.
- Remove matching `IsOpen="True"` defaults from the standalone AXAML interaction samples. Status: completed.
- Add explicit small demo controls where needed: Dropdown `Open`/`Dismiss`, Accordion `Open routing`/`Open billing`/`Collapse all`, and Collapsible `Toggle`/`Reduce motion`. Status: completed.
- Add `NavigationInteractionDocsDoNotStartDisclosureExamplesOpen` to guard MainWindow methods, companion C# samples, and standalone AXAML interaction samples against initial-open regressions. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C192: Overlay Interaction Examples Default Closed

Status: completed for this default-behavior Docs pass. This slice extends the default-closed cleanup into Overlay interaction examples so Dialog, Popover, Sheet, Drawer, and CommandDialog no longer mount already-open layers in the interaction case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DialogInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/PopoverInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/SheetInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DrawerInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/CommandDialogInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/PopoverInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/SheetInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/DrawerInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/CommandDialogInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- A read-only local Agent audited only the Overlay interaction methods and companion C# samples. It flagged initial-open object initializers in Sheet, Drawer, CommandDialog, and Popover, then stalled after producing usable line-level evidence and was terminated.
- Local AXAML inspection found matching `IsOpen="True"` in Dialog, Popover, Sheet, Drawer, and CommandDialog interaction samples.
- Web-style overlay examples should start closed and open through trigger, keyboard, or an explicit demo command; States and Anatomy examples remain free to display open surfaces.

Implementation targets:

- Remove initial `IsOpen=true` from Dialog, Popover, Sheet, Drawer, and CommandDialog interaction previews where the layer was mounted open. Status: completed.
- Remove matching `IsOpen="True"` defaults from the standalone AXAML Overlay interaction samples. Status: completed.
- Add or preserve explicit triggers for manual/persistent/action overlay variants so default-closed samples still expose the intended interaction path. Status: completed.
- Update companion C# samples for Popover, Sheet, Drawer, and CommandDialog so their sample code matches the default-closed behavior shown by Docs. Status: completed.
- Add `OverlayInteractionDocsDoNotStartLayerExamplesOpen` to guard MainWindow methods, companion C# samples, and standalone AXAML interaction samples against initial-open regressions. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C193: Remaining Overlay Interaction Examples Default Closed

Status: completed for this default-behavior Docs pass. This slice completes the Overlay interaction default-closed cleanup for AlertDialog, Tooltip, and HoverCard after C192 handled Dialog, Popover, Sheet, Drawer, and CommandDialog.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/AlertDialogInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/TooltipInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/HoverCardInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/AlertDialogInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/TooltipInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/HoverCardInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- A read-only local Agent audited only `BuildAlertDialogInteractionPreview`, `BuildTooltipInteractionPreview`, `BuildHoverCardInteractionPreview`, and the matching AXAML/C# interaction samples; it identified initial-open examples in AlertDialog, Tooltip, and HoverCard before stalling and being terminated.
- Local inspection confirmed `IsOpen=true` / `IsOpen="True"` in AlertDialog async/manual policy, Tooltip persistent/large examples, and HoverCard default/instant-focus examples.
- These are interaction examples, so they should demonstrate open behavior through trigger, delayed hover/focus, or explicit demo controls rather than being mounted open on first render.

Implementation targets:

- Remove initial `IsOpen=true` from AlertDialog async/manual, Tooltip persistent/large, and HoverCard default/instant-focus interaction previews. Status: completed.
- Remove matching `IsOpen="True"` defaults from standalone AXAML interaction samples. Status: completed.
- Add explicit triggers for default-closed AlertDialog manual policy and Tooltip persistent examples so the intended behavior remains reachable from the rendered component. Status: completed.
- Extend `OverlayInteractionDocsDoNotStartLayerExamplesOpen` to cover AlertDialog, Tooltip, and HoverCard MainWindow methods plus their C# and AXAML samples. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C194: Remaining Navigation Interaction Examples Default Closed

Status: completed for this default-behavior Docs pass. This slice finishes the Navigation interaction examples that still mounted menu-like surfaces open on first render, so the examples now match Web defaults and only disclose through hover/focus, right click, or explicit demo triggers.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/NavigationMenuInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/MenubarInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/MenuInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/ContextMenuInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/BreadcrumbInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/NavigationMenuInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/MenubarInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/MenuInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/ContextMenuInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/BreadcrumbInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- A read-only local Agent audited default-open risks across remaining Docs interaction examples and flagged NavigationMenu active values, Menubar/Menu/ContextMenu open submenus, Breadcrumb dropdown open state, plus later slices for Chart/Toast/Sonner/DataTable/Overlay primitives.
- Local inspection confirmed Navigation interaction cases were using initial `ActivateItem`, `OpenMenu`, `IsSubMenuOpen=true`, `Classes.Add("context-menu-open")`, and `IsOpen=true` patterns that made the rendered examples start already disclosed.
- Web-style navigation menus, menubars, menus, context menus, and breadcrumb overflow menus should be closed by default; docs can still provide explicit controls to demonstrate opening without changing initial behavior.

Implementation targets:

- Remove initial `ActivateItem` calls and `ActiveValue` defaults from NavigationMenu interaction previews and samples, then add explicit small `Open components` / `Open vertical` demo buttons. Status: completed.
- Remove initial `OpenMenu(file)` / submenu-open defaults from Menubar interaction previews and samples while preserving an explicit `Open view` demo trigger. Status: completed.
- Remove `IsSubMenuOpen=true` from Menu and ContextMenu interaction previews, companion C# samples, and AXAML samples. Status: completed.
- Replace context-menu-open class-based rendered surfaces with real `CodexButton.ContextMenu` trigger targets in the interaction preview and companion samples. Status: completed.
- Remove the initial open Breadcrumb overflow dropdown state and extend `NavigationInteractionDocsDoNotStartDisclosureExamplesOpen` to cover these remaining Navigation interaction families. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C195: Feedback Data And Primitive Interaction Examples Default Closed

Status: completed for this default-behavior Docs pass. This slice clears the remaining interaction examples that mounted transient surfaces or tooltip-like content immediately on render, while preserving explicit user-triggered open paths.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/ToastInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/SonnerInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ChartInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/DataTableInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Primitives/OverlayInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Feedback/ToastInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Feedback/SonnerInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/ChartInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Primitives/OverlayInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- A read-only local Agent audited the Feedback, DataDisplay, and Primitives interaction examples, then re-ran after edits and confirmed remaining open/service calls only live inside explicit click handlers.
- Local inspection found `ToastInteraction` mounting toast roots open by default, `SonnerInteraction` enqueueing notifications during preview construction, `ChartInteraction` initializing tooltip state open, `OverlayInteraction` mounting primitive overlays open, and `DataTableInteraction.axaml` opening dropdown menus statically.
- States/anatomy examples remain allowed to display open overlays, dropdowns, and chart tooltips; interaction examples should model the Web default of closed/empty until user action.

Implementation targets:

- Set Toast interaction roots to `IsOpen=false` and add explicit `Show toast` / `Show action toast` / toggle controls for interaction paths. Status: completed.
- Change Sonner interaction to build empty viewports and queue success, warning, and loading toasts only from buttons. Status: completed.
- Initialize Chart interaction tooltip state as closed and feed the first tooltip through the same controlled `tooltipOpen` state used by the toggle button. Status: completed.
- Set Overlay primitive interaction overlays to closed by default while preserving an explicit `Open` button and scrim/policy controls. Status: completed.
- Remove default-open dropdown attributes from the DataTable AXAML interaction sample and add `FeedbackDataAndPrimitiveInteractionDocsDoNotStartTransientExamplesOpen` regression coverage. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C197: Tooltip HoverCard And Popover Popup Positioning

Status: completed for this overlay-positioning parity pass. This slice removes layout-bound and content-width-guessed positioning from Tooltip, HoverCard, and Popover so all three now use a real `Popup` anchored to their trigger, with Web/shadcn-style 4px side offsets and refreshed Docs visual fingerprints.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexHoverCard.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Tooltip.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/HoverCard.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Popover.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- Local style inspection found Tooltip and HoverCard still used fixed or negative margin guesses such as `-188`, `-248`, `-112`, `38`, and `36` to simulate side placement relative to a trigger.
- A read-only local Agent audited the overlay positioning slice after the Tooltip/HoverCard edit and confirmed the next high-priority issue: Popover still used normal two-row layout instead of popup/portal-style positioning, so closed content could affect layout and side/align classes did not drive actual floating placement.
- Existing overlay behavior tests already proved open/close source metadata and default-closed behavior, so this pass focused on replacing positioning mechanics while preserving event contracts.

Implementation targets:

- Change `Tooltip.axaml` so `PART_Surface` and `PART_Arrow` live under `Popup#PART_Popup` with `PlacementTarget="PART_Trigger"` and `Placement="{TemplateBinding Placement}"`. Status: completed.
- Replace Tooltip's hardcoded trigger-position margins with side-aware 4px offsets and keep arrow offsets as visual styling only. Status: completed.
- Change `HoverCard.axaml` to the same popup anchoring model, replacing `-112`, `38`, `-248`, and arrow `-14` guesses with 4px surface offsets and small arrow overlap margins. Status: completed.
- Add HoverCard surface pointer enter/exit handlers in `CodexHoverCard.cs` so moving from trigger to popup content cancels a pending close and preserves Radix-style hoverable content behavior. Status: completed.
- Change `Popover.axaml` from a normal `RowDefinitions="Auto,Auto"` layout to a real trigger-anchored popup and replace the old `0,8,0,0` margin with side-aware 4px offsets. Status: completed.
- Add `TooltipHoverCardAndPopoverUsePopupSideOffsetPlacement` to guard `PART_Popup`, trigger placement, 4px offsets, and absence of old width/height guesses. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~TooltipHoverCardAndPopoverUsePopupSideOffsetPlacement|FullyQualifiedName~PopoverTriggerOpenChangedAndKeyboardToggleMirrorWebRoot|FullyQualifiedName~PopoverTooltipAndHoverCardOpenChangedEventsExposeSourceMetadata|FullyQualifiedName~DismissableSurfacesRequestFocusRestoreAfterClose|FullyQualifiedName~OverlayAndFeedbackStylesDeclareTemplatesAndMotion|FullyQualifiedName~OverlayMotionSurfacesExposeTokenizedTransitionsAndDismissState|FullyQualifiedName~ReducedMotionOverlaySurfacesResolveZeroDurationTransitions|FullyQualifiedName~HoverCardRenderedDelayStatesMatchWebOpenCloseTiming"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~OverlayFeedbackComponentTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C196: Popup Disclosure Side Offset And Form Row Density

Status: completed for this spacing parity pass. This slice aligns popup/disclosure side offsets and form popup row density with the Web/shadcn 4px side-offset and compact 32px menu-row rhythm, then locks the contract with targeted style tests and refreshed Docs visual fingerprints.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/DropdownButton.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/SplitButton.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Select.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Combobox.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/DatePicker.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Menu.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Menubar.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- Web docs/home surfaces use the established 4/8/12/16/20 spacing rhythm, and shadcn/Radix popup content commonly uses a 4px side offset rather than the older 6px desktop offset.
- The theme token set already exposes `CodexSwitch.PopoverEnterOffset = 4`, but several popup templates still had hardcoded `Margin="0,6,0,0"` or 6px side placement values.
- A read-only local Agent audited the edited popup files and confirmed target popup offsets were 4px; it also flagged two test precision gaps, so this pass tightened assertions for Menu/Menubar default right-side submenus and item style blocks.

Implementation targets:

- Change DropdownButton and SplitButton popup surface margins, side placement setters, and entrance transforms from 6px to 4px. Status: completed.
- Change Select, Combobox, and DatePicker popup border margins from `0,6,0,0` to `0,4,0,0`. Status: completed.
- Change Menu and Menubar top-level/side submenu popup offsets and entrance transforms to 4px, including the Menubar base submenu transform. Status: completed.
- Change Select popup item padding from `10,7` to `8,6` in both the item container theme and fallback item style. Status: completed.
- Change Combobox item row density from `MinHeight=34` / `Padding=8,7` to `MinHeight=32` / `Padding=8,6`. Status: completed.
- Add targeted Form and Navigation style tests for 4px popup offsets, precise Select/Combobox row density, and absence of stale 6px/34px form-popup values. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~NavigationDataComponentTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DropdownAndSplitButtonPopupMotionAndTriggerStateMatchWebMenuContract|FullyQualifiedName~ReducedMotionDropdownAndSplitButtonPopupTransitionsResolveZeroDuration|FullyQualifiedName~SelectNavigationMenuAndCollapsibleChevronMotionSelectorsHitMountedTemplates|FullyQualifiedName~MenuAndContextMenuSubMenuMotionSelectorsHitMountedTemplates"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C189: Button And Navigation Action Row Web Spacing

Status: completed for this spacing-parity pass. This slice aligns desktop action rows with the local Web home page CTA and link-row evidence, and keeps the Docs visual baseline updated after the spacing changes.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Button.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ButtonGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/NavigationMenu.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ApplicationShell.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- `docs-site/app/[lang]/(home)/page.tsx` uses CTA buttons with `inline-flex h-11 items-center gap-2 rounded-md ... px-5 text-sm font-medium`, mapping to Avalonia `44` height, `20` horizontal padding, `8` content gap, and `16` icons.
- The same Web page uses link rows with `inline-flex items-center gap-2 rounded-md border px-4 py-3 text-sm font-medium`, mapping to Avalonia `16,12` padding and `8` icon/text spacing.
- Web list rows use `flex items-center justify-between px-4 py-3 text-sm`, so desktop side navigation items should share the same row rhythm rather than the earlier denser `10,8` shell-local spacing.

Implementation targets:

- Set default `CodexButton` and `CodexButton.size-icon` action height to `44`, default button padding to `20,0`, and preserve the existing `8` content spacing. Status: completed.
- Align `CodexButtonGroupText` with the same `44` action height and `20,0` horizontal padding so grouped text actions do not drift from standalone buttons. Status: completed.
- Align `CodexNavigationMenuLink` rows to `44` minimum height, `16,12` padding, `8` column spacing, and `16` icon sizing. Status: completed.
- Align `CodexSideNavItem` to the Web list-row rhythm with `44` minimum height, `16,12` padding, and `8` icon/text spacing. Status: completed.
- Add `ButtonsAndNavigationLinksUseWebActionRowSpacing` so future theme edits cannot quietly regress these spacing contracts. Status: completed.

Agent usage:

- A read-only local verification Agent was launched with a narrow prompt covering only the five files in this slice. It did not produce output after more than two hours, so it was terminated and the bounded local test/visual gates below were used as the source of truth for this pass.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~FormComponentDetailTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C186: Default Closed Docs Examples And Web Menu Spacing

Status: completed for this default-behavior and menu-density slice. This pass keeps Web-style disclosure surfaces closed by default in the default Docs examples and preview builders, keeps intentionally visible surfaces such as Toast and Sidebar open, and aligns menu item density with shadcn/Radix menu spacing.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexChart.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Primitives/CodexOverlay.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Menu.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ContextMenu.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Menubar.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/**`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- Web/Radix overlay, tooltip, popover, dropdown, navigation menu, collapsible, accordion, and chart tooltip surfaces are hover/trigger/state driven and should not mount open by default.
- Toast creation and desktop sidebar layout are intentionally visible by default in the desktop shell, so those defaults remain open.
- shadcn menu content uses `p-1`, items use `px-2 py-1.5`, and separators use `-mx-1 my-1`, mapping to Avalonia menu content padding 4, item padding `8,6`, and separator margin `-4,4`.
- The read-only Agent audit caught that the default Overlay Docs sample became visually trigger-like without actual open wiring after switching the primitive to default closed; the preview and AXAML sample now provide a real user-triggered open path.

Implementation targets:

- Remove forced `IsOpen`, `IsSubMenuOpen`, `IsDropDownOpen`, `IsExpanded`, `ActivateItem`, `OpenMenu`, and `context-menu-open` usage from default Docs examples and preview builders while leaving states/anatomy/interaction examples free to show explicit open states. Status: completed.
- Change `CodexChartTooltipContent` and primitive `CodexOverlay` defaults to closed. Status: completed.
- Convert the default ContextMenu example to a trigger-owned context menu instead of an always-open standalone menu. Status: completed.
- Make the default Overlay example triggerable while keeping its initial state closed. Status: completed.
- Align Menu, ContextMenu, and Menubar item/inset/separator spacing to Web menu density. Status: completed.
- Add tests guarding default-closed disclosure samples, default component closed/open exceptions, and Web menu spacing constants. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~NavigationDataComponentTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C187: Card EmptyState Field And Sheet Web Panel Spacing

Status: completed for this panel-density slice. This pass tightens the high-traffic content surfaces that were still wider than the Web project panels, while preserving Dialog and Sheet outer `p-6` overlay padding where it matches the shadcn overlay contract.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Card.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/EmptyState.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Field.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Sheet.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- Local Web home/docs surfaces use `p-4` and `p-5` for repeated panels/cards, `gap-4` for content rhythm, and `space-y-2` style form spacing.
- `CodexCard` still used `Padding=24`, making regular cards read like overlay dialogs instead of Web panels.
- `CodexEmptyState` used `Padding=28/36` and tall `MinHeight=180/220`, which made empty/no-result panels noticeably looser than the Web panel density.
- `CodexField` used `RowSpacing=6` and `FieldGroup`/`FieldSet` vertical spacing `14`, missing the Web `space-y-2` and `gap-4` cadence.
- `CodexSheet` kept overlay padding at `24`, but its internal row spacing was `18` while Dialog and Web `gap-4` use `16`.

Implementation targets:

- Align Card default padding to Web `p-5` (`20`). Status: completed.
- Align EmptyState default/small/large padding to `20/16/24` and min-heights to `160/128/200`. Status: completed.
- Align Field vertical rhythm to `RowSpacing=8`, with small/icon/large at `6/4/10`, and FieldGroup/FieldSet vertical rhythm to `16`. Status: completed.
- Align Sheet internal header/content/action row spacing to `16` while preserving overlay padding `24`. Status: completed.
- Add a source guard for Card, EmptyState, Field, Dialog, and Sheet spacing so future edits cannot drift back to the old looser values. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C188: Docs Shell Code Block And Matrix Web Spacing

Status: completed for this Docs density slice. This pass aligns the documentation shell and inline source reveal areas with the local Web spacing cadence so every component page inherits the same desktop gutter, vertical rhythm, code-block padding, and behavior-row density.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Controls/DocsCodeBlock.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- The local Web home/docs surfaces use `px-5 py-14 md:px-8 lg:px-10` for page containers, so the desktop Docs shell should map to a 40 px gutter and 56 px vertical page padding.
- The Web code block uses `rounded-md bg-muted p-4`, so the Avalonia `DocsCodeBlock` needs a 16 px content inset rather than the previous 2 px top/bottom code padding.
- Web repeated link/row actions use `px-4 py-3`, so the Docs behavior matrix and notes should use 16/12 padding instead of the tighter 10/8 cells.
- Inline examples already render preview first, then `Show code` / `Hide code`, then copyable code blocks; this slice keeps that behavior and only tightens the visual rhythm around it.

Implementation targets:

- Add Docs shell spacing constants for desktop gutter, vertical page padding, inline example gap, and matrix cell padding. Status: completed.
- Change the Docs content scroll padding to `40,56,40,56` and the stable topbar gutter to 40. Status: completed.
- Align inline example and code reveal stack spacing to 16. Status: completed.
- Align behavior notes and state/event matrix cells to `px-4 py-3` density. Status: completed.
- Align `DocsCodeBlock` titlebar/content insets and line-number column with a Web-style `p-4` source block. Status: completed.
- Add source guards for the new spacing constants and code-block inset contract. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

Next audit slice:

- Continue with a narrow Agent for remaining button/link row height and gap parity against Web `h-11 px-5 gap-2`, plus motion/event deltas on the highest-traffic controls.

### Agent C168: Menubar Anatomy Docs Case

Status: completed for this Navigation Docs coverage slice. This pass splits Menubar anatomy out of the broader composition example so the docs page exposes an independent Root/Trigger/Content anatomy case with its own inline `Show code` / `Hide code` AXAML sample.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/MenubarAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- A read-only subAgent audit confirmed `navigation.menubar` had default, states, composition, and interaction examples, but no independent Anatomy example.
- `BuildInlineExample` already renders the component preview before the local code toggle and `DocsCodeBlock`, so the missing work is registering a distinct sample and builder.
- Menubar's public composition surface includes root orientation/loop, top-level trigger menus, popup content, labels, groups, separators, shortcut rows, checkbox/radio rows, disabled rows, and nested submenu content.

Implementation targets:

- Add `MenubarAnatomy.axaml` with root/trigger/content, label, group, separator, shortcut, disabled row, checkbox, radio, nested submenu, and vertical menubar anatomy. Status: completed.
- Add `BuildMenubarAnatomyPreview` and register it under `navigation.menubar` between states and composition. Status: completed.
- Strengthen Docs panel tests so Menubar anatomy is independently registered and exposes the public primitives directly. Status: completed.
- Update docs-site Navigation pages in English and Chinese to mention Menubar anatomy coverage. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C169: Resizable Anatomy Docs Case

Status: completed for this Layout Docs coverage slice. This pass adds an independent Resizable anatomy example so `layout.resizable` no longer relies on the composition sample to explain the shadcn-style `PanelGroup`, `Panel`, and `Handle` structure.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/ResizableAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/index.mdx`
- `docs-site/content/docs/zh/ui-system/index.mdx`

Evidence gathered:

- `layout.resizable` had default, states, composition, and interaction examples, but no standalone Anatomy example.
- The control and style already expose the required parts: panel group, panels, handles, handle track, visible grip, focus ring, orientation, size, dragging, min/max constraints, and layout summary.
- Existing Docs inline code plumbing already renders each case before its local code reveal, so this slice only needs a distinct sample, builder, registration, and tests.

Implementation targets:

- Add `ResizableAnatomy.axaml` covering panel group anatomy, visible and hidden handle grips, vertical orientation, and min/max constraints. Status: completed.
- Add `BuildResizableAnatomyPreview` and register it under `layout.resizable` between states and composition. Status: completed.
- Strengthen Docs panel tests so Resizable anatomy is independently registered and exposes public `CodexResizablePanelGroup`, `CodexResizablePanel`, and `CodexResizableHandle` primitives. Status: completed.
- Update docs-site UI overview pages in English and Chinese to mention Resizable anatomy coverage. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C170: Forms Composition Anatomy Docs Cases

Status: completed for this Forms Docs coverage slice. This pass splits anatomy coverage out of composition samples for `forms.button-group`, `forms.input-group`, `forms.input-otp`, and `forms.label` so public structure primitives are visible as their own examples with local inline AXAML source reveal.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ButtonGroupAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/InputGroupAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/InputOtpAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/LabelAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- A read-only subAgent was launched to audit the four Forms pages while local inspection confirmed each page had default, states, composition, and interaction cases but no standalone Anatomy case.
- Button Group public anatomy includes root group, connected item positions, text segments, separators, orientation, nested group spacing, and mixed input/select controls.
- Input Group public anatomy includes root border/focus ring, inline and block addons, input, textarea, text, button, select, intent, and focus-within styling.
- Input OTP public anatomy includes root text/pattern/max length, groups, slots, separators, active and invalid slots, size, and recovery-code grouping.
- Label public anatomy includes target association, required marker, access-key text, intent styling, target-disabled styling, and template-owned marker rendering.

Implementation targets:

- Add independent Anatomy AXAML files for Button Group, Input Group, Input OTP, and Label. Status: completed.
- Add `BuildButtonGroupAnatomyPreview`, `BuildInputGroupAnatomyPreview`, `BuildInputOtpAnatomyPreview`, and `BuildLabelAnatomyPreview`, then register them between states and composition. Status: completed.
- Strengthen Docs panel tests so these public primitives are independently registered and directly present in copied AXAML samples. Status: completed.
- Update docs-site Forms pages in English and Chinese to mention the new anatomy split. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint`
- `npm run build`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C171: Docs Interaction Naming Alignment

Status: completed for this Docs naming and coverage-alignment slice. Read-only subAgents audited the registered Docs cases and confirmed the remaining high-signal drift is not missing rendered controls, but misleading case names that make interaction coverage look absent or attached to the wrong component.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/SonnerInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- `forms.switch` registered `Forms/SwitchStates.axaml` as `Toggle states`, which made the Switch page look like it reused Toggle coverage.
- `feedback.spinner` registered `Feedback/SpinnerInteraction.axaml` as `Spinner lifecycle`, even though the file and builder are the independent interaction case.
- `feedback.sonner` had an independent lifecycle case, but the `SonnerLifecycle` filename and builder kept it out of the explicit Interaction naming pattern used by the rest of the inline-expandable gallery.
- `feedback.skeleton` registered `Feedback/SkeletonInteraction.axaml` as `Skeleton motion`, leaving the interaction sample outside the same public Docs taxonomy.

Implementation targets:

- Rename the Switch states case title to `Switch states`. Status: completed.
- Rename the Spinner case title to `Spinner interaction` while preserving its existing AXAML and builder. Status: completed.
- Rename Sonner lifecycle wiring to `Sonner interaction`, `Feedback/SonnerInteraction.axaml`, and `BuildSonnerInteractionPreview`. Status: completed.
- Rename the Skeleton interaction case title to `Skeleton interaction` while preserving its existing AXAML and builder. Status: completed.
- Strengthen Docs panel tests so Sonner and Skeleton are guarded as interaction samples. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`

### Agent C172: Sonner Service Lifecycle Docs Matrix

Status: completed for this Feedback Docs service-lifecycle slice. A read-only feedback Agent confirmed that Sonner is now registered as an interaction case, but its state and event matrices still inherited generic Toast entries rather than documenting the Web-style Sonner service queue contract.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/feedback.mdx`
- `docs-site/content/docs/zh/ui-system/feedback.mdx`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- `feedback.sonner` renders an inline expandable interaction example, but Sonner behavior is service-driven through `CodexSonnerService` rather than only a single toast's `IsOpen` property.
- The existing state matrix grouped `feedback.toast` and `feedback.sonner`, so queue insertion, auto-dismiss timers, closing/removal, stacking, rich colors, and close-button visibility were not visible as Sonner-specific review rows.
- The existing event matrix also grouped `feedback.toast` and `feedback.sonner`, so Show, Dismiss, Clear, option actions, limit trimming, and loading duration behavior were not visible as Sonner-specific event rows.

Implementation targets:

- Split `feedback.sonner` out from generic Toast state rows and document service queue, auto-dismiss, closing, stacking, rich colors, and close visibility. Status: completed.
- Split `feedback.sonner` out from generic Toast event rows and document Show, Dismiss, Clear, action/cancel, ToastLimit trimming, and loading duration behavior. Status: completed.
- Strengthen Docs panel tests so the Sonner service lifecycle rows cannot regress back to generic Toast entries. Status: completed.
- Update docs-site Feedback summaries in English and Chinese to include Sonner service lifecycle review criteria. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint`
- `npm run build`

### Agent C173: Docs Companion CSharp Source Blocks

Status: completed for this Docs inline-source parity slice. This pass keeps the existing local `Show code` / `Hide code` workflow, but allows a rendered case to expose more than one source block when the preview behavior depends on hidden C# service or event wiring rather than AXAML alone.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/CodexSwitchUI.Docs.csproj`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Docs/DocsPage.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Docs/DocsCodeSamples.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Feedback/AlertInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Feedback/BadgeInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Feedback/AvatarInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Feedback/AvatarGroupInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Feedback/SonnerInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Feedback/ToastInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/DialogInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/AlertDialogInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/SheetInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/DrawerInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/CommandDialogInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/PopoverInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/TooltipInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Overlay/HoverCardInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/NavigationMenuInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/MenubarInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/CommandInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/TabsInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/BreadcrumbInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/SideNavInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/DropdownButtonInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/MenuInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/ContextMenuInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Primitives/OverlayInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/SelectInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/ComboboxInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/NativeSelectInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/CalendarInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/DatePickerInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overview/GettingStartedAnatomy.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- A read-only audit Agent found that `feedback.sonner` is the highest-priority service-driven Docs page where the rendered preview depends on hidden `CodexSonnerService` calls while the inline source previously showed only the viewport AXAML.
- The same audit identified follow-up companion-source candidates for Toast, AlertDialog, Dialog, Drawer, Sheet, CommandDialog, Menubar, NavigationMenu, and Command interaction examples.
- A second read-only audit pass confirmed that `feedback.toast`, `overlay.dialog`, and `overlay.alert-dialog` are the next strongest hidden-C# candidates because their rendered previews exercise command callbacks, dismiss commands, open-change events, focus-return requests, manual close policies, loading suppression, and closed exit state toggles.
- A focused read-only extraction Agent confirmed that `overlay.sheet`, `overlay.drawer`, and `overlay.command-dialog` also need companion C# because their previews depend on dismiss commands, side/direction switching, manual Escape/outside policies, drag-completion events, close-on-select, item selection metadata, loading suppression, and focus restoration.
- A Navigation-focused read-only extraction Agent confirmed that `navigation.navigation-menu`, `navigation.menubar`, and `navigation.command` need companion C# because their rendered previews depend on `ActiveItemChanged`, link activation, `ActivateItem`/`CloseViewport`, `ItemSelected`, `ActiveMenuChanged`, `OpenMenu`/`Dismiss`, keyboard navigation, loading suppression, and command `CanExecute` guards.
- A second Navigation read-only extraction pass confirmed that `navigation.tabs`, `navigation.breadcrumb`, `navigation.side-nav`, `navigation.dropdown`, `navigation.menu`, and `navigation.context-menu` also need companion C# because their rendered previews depend on source-aware `ValueChanged`, `LinkActivated`, `OpenChanged`, restore-focus, close-on-select, menu `ItemSelected`, submenu placement, loading suppression, and command `CanExecute` guards.
- A focused Overlay/Primitives read-only extraction Agent confirmed that `overlay.popover`, `overlay.tooltip`, `overlay.hover-card`, and `primitives.overlay` need companion C# because their previews depend on `OpenChanged`, `DismissCommand`, focus return, manual Escape/outside policy, provider delay, open/close delays, focus/hover open paths, scrim toggling, and primitive overlay dismiss state.
- A focused Feedback companion-source audit confirmed that `feedback.alert`, `feedback.badge`, `feedback.avatar`, and `feedback.avatar-group` still needed companion C# because their previews depend on slotted action mutation, slot-presence classes, source-aware badge activation, command-blocked badges, avatar loading lifecycle events, fallback delay changes, stacked/inline group layout, overlap changes, visibility filtering, and group `ItemCount`.
- A focused Forms select/date companion-source audit confirmed that `forms.select`, `forms.combobox`, `forms.native-select`, `forms.calendar`, and `forms.date-picker` need companion C# because their previews depend on source-aware open/value/selection/input events, keyboard selection, clear/reset, loading suppression, disabled options, selected/range/month/active date events, command-blocked day buttons, and picker range completion.
- Existing `DocsExampleCase` carried only `SamplePath`, so `BuildInlineExample` could not show the extra C# required to understand service-driven examples.

Implementation targets:

- Add `DocsCodeSnippet` and optional `AdditionalCodeSamples` to `DocsExampleCase` while preserving the default AXAML source block for every existing example. Status: completed.
- Generalize `DocsCodeSamples.Load` so it can load copied files from `Examples/Axaml` and `Examples/CSharp`. Status: completed.
- Exclude `Examples/CSharp/**/*.cs` from compilation and copy those samples to the Docs output as selectable source artifacts. Status: completed.
- Add `Examples/CSharp/Feedback/AlertInteraction.cs` and wire it as a companion source block under the `feedback.alert` interaction case. Status: completed.
- Add `Examples/CSharp/Feedback/BadgeInteraction.cs` and wire it as a companion source block under the `feedback.badge` interaction case. Status: completed.
- Add `Examples/CSharp/Feedback/AvatarInteraction.cs` and wire it as a companion source block under the `feedback.avatar` interaction case. Status: completed.
- Add `Examples/CSharp/Feedback/AvatarGroupInteraction.cs` and wire it as a companion source block under the `feedback.avatar-group` interaction case. Status: completed.
- Add `Examples/CSharp/Feedback/SonnerInteraction.cs` and wire it as a companion source block under the `feedback.sonner` interaction case. Status: completed.
- Add `Examples/CSharp/Feedback/ToastInteraction.cs` and wire it as a companion source block under the `feedback.toast` interaction case. Status: completed.
- Add `Examples/CSharp/Overlay/DialogInteraction.cs` and wire it as a companion source block under the `overlay.dialog` interaction case. Status: completed.
- Add `Examples/CSharp/Overlay/AlertDialogInteraction.cs` and wire it as a companion source block under the `overlay.alert-dialog` interaction case. Status: completed.
- Add `Examples/CSharp/Overlay/SheetInteraction.cs` and wire it as a companion source block under the `overlay.sheet` interaction case. Status: completed.
- Add `Examples/CSharp/Overlay/DrawerInteraction.cs` and wire it as a companion source block under the `overlay.drawer` interaction case. Status: completed.
- Add `Examples/CSharp/Overlay/CommandDialogInteraction.cs` and wire it as a companion source block under the `overlay.command-dialog` interaction case. Status: completed.
- Add `Examples/CSharp/Navigation/NavigationMenuInteraction.cs` and wire it as a companion source block under the `navigation.navigation-menu` interaction case. Status: completed.
- Add `Examples/CSharp/Navigation/MenubarInteraction.cs` and wire it as a companion source block under the `navigation.menubar` interaction case. Status: completed.
- Add `Examples/CSharp/Navigation/CommandInteraction.cs` and wire it as a companion source block under the `navigation.command` interaction case. Status: completed.
- Add `Examples/CSharp/Navigation/TabsInteraction.cs` and wire it as a companion source block under the `navigation.tabs` interaction case. Status: completed.
- Add `Examples/CSharp/Navigation/BreadcrumbInteraction.cs` and wire it as a companion source block under the `navigation.breadcrumb` interaction case. Status: completed.
- Add `Examples/CSharp/Navigation/SideNavInteraction.cs` and wire it as a companion source block under the `navigation.side-nav` interaction case. Status: completed.
- Add `Examples/CSharp/Navigation/DropdownButtonInteraction.cs` and wire it as a companion source block under the `navigation.dropdown` interaction case. Status: completed.
- Add `Examples/CSharp/Navigation/MenuInteraction.cs` and wire it as a companion source block under the `navigation.menu` interaction case. Status: completed.
- Add `Examples/CSharp/Navigation/ContextMenuInteraction.cs` and wire it as a companion source block under the `navigation.context-menu` interaction case. Status: completed.
- Add `Examples/CSharp/Overlay/PopoverInteraction.cs` and wire it as a companion source block under the `overlay.popover` interaction case. Status: completed.
- Add `Examples/CSharp/Overlay/TooltipInteraction.cs` and wire it as a companion source block under the `overlay.tooltip` interaction case. Status: completed.
- Add `Examples/CSharp/Overlay/HoverCardInteraction.cs` and wire it as a companion source block under the `overlay.hover-card` interaction case. Status: completed.
- Add `Examples/CSharp/Primitives/OverlayInteraction.cs` and wire it as a companion source block under the `primitives.overlay` interaction case. Status: completed.
- Add `Examples/CSharp/Forms/SelectInteraction.cs` and wire it as a companion source block under the `forms.select` interaction case. Status: completed.
- Add `Examples/CSharp/Forms/ComboboxInteraction.cs` and wire it as a companion source block under the `forms.combobox` interaction case. Status: completed.
- Add `Examples/CSharp/Forms/NativeSelectInteraction.cs` and wire it as a companion source block under the `forms.native-select` interaction case. Status: completed.
- Add `Examples/CSharp/Forms/CalendarInteraction.cs` and wire it as a companion source block under the `forms.calendar` interaction case. Status: completed.
- Add `Examples/CSharp/Forms/DatePickerInteraction.cs` and wire it as a companion source block under the `forms.date-picker` interaction case. Status: completed.
- Update the Docs overview anatomy sample to describe per-case source blocks rather than a single AXAML-only block. Status: completed.
- Strengthen static and rendered Docs tests so companion C# source blocks remain copyable and do not break the expanded-code gallery. Status: completed.

Follow-up audit queue:

- No Feedback, Overlay, Navigation, Primitive, or Forms select/date hidden-behavior candidates remain in this tracked companion-source batch.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`

### Agent C174: Forms Control Companion CSharp Source Blocks

Status: completed for this Forms companion-source pass. This pass extends the per-case inline source reveal from AXAML-only interaction examples to include the C# event wiring for the remaining high-risk Forms controls whose Docs previews depend on hidden checked, pressed, value, and commit handlers.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/CheckboxInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/RadioInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/RadioGroupInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/SwitchInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/ToggleInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/ToggleGroupInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/SliderInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- A focused read-only audit pass confirmed that `forms.checkbox`, `forms.radio`, `forms.radio-group`, `forms.switch`, `forms.toggle`, `forms.toggle-group`, and `forms.slider` still rendered interaction behavior from hidden C# while the local inline code reveal showed only AXAML.
- The hidden preview wiring covers `CheckedStateChanged`, `Checked`, `ValueChanged`, `CheckedChanged`, `PressedChanged`, `ValueChanging`, `ValueCommitted`, source metadata, three-state cycling, programmatic state changes, disabled guards, loading suppression, roving keys, loop boundaries, multiple selection, vertical orientation, and slider commit paths.
- Existing `DocsExampleCase.CodeSamples` already supports multiple source blocks per rendered case, so this pass only needed companion files plus `Code(...)` registration.

Implementation targets:

- Add and wire `Examples/CSharp/Forms/CheckboxInteraction.cs` under the `forms.checkbox` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/RadioInteraction.cs` under the `forms.radio` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/RadioGroupInteraction.cs` under the `forms.radio-group` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/SwitchInteraction.cs` under the `forms.switch` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/ToggleInteraction.cs` under the `forms.toggle` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/ToggleGroupInteraction.cs` under the `forms.toggle-group` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/SliderInteraction.cs` under the `forms.slider` interaction case. Status: completed.
- Strengthen Docs static tests so these companion C# files remain registered, copied, and content-checked. Status: completed.
- Refresh Docs visual fingerprints after expanded inline source gained additional C# blocks. Status: completed.

Follow-up audit queue:

- Continue companion C# coverage for Data Display interaction examples that still depend on hidden pointer, command, selection, paging, carousel, or chart refresh handlers.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C175: Layout Companion CSharp Source Blocks

Status: completed for this Layout companion-source pass. This pass extends the per-case inline source reveal for Layout interaction examples so shell, sidebar, section, and resizable behavior can be understood from the local `Show code` block instead of only from hidden Docs C# preview wiring.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Layout/ApplicationShellInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Layout/SidebarInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Layout/SidebarPrimitivesInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Layout/SectionInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Layout/ResizableInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- A focused read-only Layout audit confirmed that `layout.application-shell`, `layout.sidebar`, `layout.sidebar-primitives`, `layout.section`, and `layout.resizable` still rendered interaction behavior from hidden C# while their local inline source reveal showed only AXAML.
- The hidden preview wiring covers sidebar active row selection, sibling clearing, badge updates, provider `OpenChanged`, `TryHandleShortcut`, trigger/rail command blocking, hover actions, nested row activation, section title/description/action/content slot mutation, resizable `LayoutChanged`, `ResizeHandleByPercent`, keyboard resize, vertical orientation, and min/max clamp behavior.
- Existing multi-source `DocsExampleCase.CodeSamples` support made this a registration and companion-file pass rather than a Docs shell change.

Implementation targets:

- Add and wire `Examples/CSharp/Layout/ApplicationShellInteraction.cs` under the `layout.application-shell` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Layout/SidebarInteraction.cs` under the `layout.sidebar` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Layout/SidebarPrimitivesInteraction.cs` under the `layout.sidebar-primitives` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Layout/SectionInteraction.cs` under the `layout.section` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Layout/ResizableInteraction.cs` under the `layout.resizable` interaction case. Status: completed.
- Strengthen Docs static tests so these Layout companion C# files remain registered, copied, and content-checked. Status: completed.

Follow-up audit queue:

- Continue companion C# coverage for Data Display and chart interaction examples that still depend on hidden pointer, command, carousel, pagination, table, or chart refresh handlers.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C176: Data Display Companion CSharp Source Blocks

Status: completed for this Data Display companion-source pass. This pass extends the per-case inline source reveal for event-heavy Data Display interaction examples so the local `Show code` block includes both the visible AXAML scaffold and the C# event, command, and state wiring that drives the rendered preview.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/CardInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/ItemInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/CarouselInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/ProviderCardInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/PaginationInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/TableInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/DataTableInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/PinnedTableInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- A bounded read-only Agent audit was attempted for the Data Display interaction previews; it confirmed the `Examples/CSharp/DataDisplay` gap, but was terminated after it began dumping large source context instead of a concise checklist.
- Focused source reads confirmed that Card, Item, Carousel, ProviderCard, Pagination, Table, DataTable, and PinnedTable interaction cases still depended on hidden Docs C# for pointer release, source-aware events, command guards, selection, pagination, table transition keys, and pinned-table refresh/density behavior.
- Existing multi-source `DocsExampleCase.CodeSamples` support made this a companion-file and registration pass, preserving the rule that each rendered component appears first and its local code expands directly underneath.

Implementation targets:

- Add and wire `Examples/CSharp/DataDisplay/CardInteraction.cs` under the `data.card` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/ItemInteraction.cs` under the `data.item` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/CarouselInteraction.cs` under the `data.carousel` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/ProviderCardInteraction.cs` under the `data.provider-card` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/PaginationInteraction.cs` under the `data.pagination` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/TableInteraction.cs` under the `data.table` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/DataTableInteraction.cs` under the `data.data-table` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/PinnedTableInteraction.cs` under the `data.pinned-table` interaction case. Status: completed.
- Strengthen Docs static tests so these Data Display companion C# files remain registered, copied, and content-checked. Status: completed.

Follow-up audit queue:

- Continue companion C# coverage for the remaining Data Display chart/metric/image/scroll interaction examples that still depend on hidden refresh, lifecycle, tooltip, or active-item handlers.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C177: Remaining Data Display Companion CSharp Source Blocks

Status: completed for this remaining Data Display companion-source pass. This pass closes the interaction-code gap left after C176 by wiring the chart, metric, image, scroll, aspect-ratio, and trend cases to local C# source blocks under their own `Show code` toggles.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/AspectRatioInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/ChartInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/BarChartInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/LineChartInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/MetricInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/ImageIconInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/ScrollAreaInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/RankedBarChartInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/UsagePieChartInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/DataDisplay/UsageTrendChartInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- Current-source audit showed these remaining `DataDisplay/*Interaction.axaml` cases still rendered behavior from hidden Docs C# while their local source reveal showed only AXAML.
- A very narrow read-only Agent was launched for these 10 methods, but it produced an empty output file; current source and control API reads were used as authoritative evidence instead.
- The missing hidden behavior covered `RatioChanged`, chart refresh and tooltip/legend state, bar/line active item events, metric slot mutation, image load/error lifecycle, scroll metrics and boundaries, ranked/usage active item events, and trend refresh/granularity/empty-state changes.

Implementation targets:

- Add and wire `Examples/CSharp/DataDisplay/AspectRatioInteraction.cs` under the `data.aspect-ratio` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/ChartInteraction.cs` under the `data.chart` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/BarChartInteraction.cs` under the `data.bar-chart` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/LineChartInteraction.cs` under the `data.line-chart` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/MetricInteraction.cs` under the `data.metric` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/ImageIconInteraction.cs` under the `data.image-icon` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/ScrollAreaInteraction.cs` under the `data.scroll-area` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/RankedBarChartInteraction.cs` under the `data.ranked-bar-chart` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/UsagePieChartInteraction.cs` under the `data.usage-pie-chart` interaction case. Status: completed.
- Add and wire `Examples/CSharp/DataDisplay/UsageTrendChartInteraction.cs` under the `data.usage-trend-chart` interaction case. Status: completed.
- Strengthen Docs static tests so every Data Display interaction companion C# file remains registered, copied, and content-checked. Status: completed.

Follow-up audit queue:

- Audit the remaining non-DataDisplay interaction pages for hidden C# behavior, especially Forms basic text/button/input cases, Feedback loading surfaces, Navigation simple disclosure cases, Primitives, and Tokens.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C178: Forms Foundation Companion CSharp Source Blocks

Status: completed for this Forms foundation companion-source pass. This pass closes the hidden C# source gap for the high-traffic basic form controls: buttons, grouped buttons, input groups, OTP input, labels, icon buttons, split buttons, fields, text boxes, and textareas now expose their event and state wiring directly under each rendered `Interaction` example.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/ButtonInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/ButtonGroupInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/InputGroupInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/InputOtpInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/LabelInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/IconButtonInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/SplitButtonInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/FieldInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/TextBoxInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Forms/TextareaInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- Current-source audit showed these `Forms/*Interaction.axaml` cases still rendered behavior from hidden Docs C# while the local inline source reveal showed only AXAML.
- The hidden behavior covered button click/command activation, loading suppression, command-blocked states, grouped child activation, input-group add-on button activation, OTP insertion and active-slot movement, label target association, icon-button loading and round state, split-button `OpenChanged` and focus restore, field validation handoff, and text input `TextChanged` / selection / read-only / disabled paths.
- A first read-only Agent invocation failed because the local `codex exec` did not support the requested `--ask-for-approval` flag; a corrected read-only Agent audit was launched after implementation to verify this specific Forms foundation registration/file set.
- Existing multi-source `DocsExampleCase.CodeSamples` support made this a companion-file and registration pass while preserving the user-required order: component preview first, local `Show code` / `Hide code`, then copyable code blocks.

Implementation targets:

- Add and wire `Examples/CSharp/Forms/ButtonInteraction.cs` under the `forms.button` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/ButtonGroupInteraction.cs` under the `forms.button-group` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/InputGroupInteraction.cs` under the `forms.input-group` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/InputOtpInteraction.cs` under the `forms.input-otp` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/LabelInteraction.cs` under the `forms.label` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/IconButtonInteraction.cs` under the `forms.icon-button` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/SplitButtonInteraction.cs` under the `forms.split-button` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/FieldInteraction.cs` under the `forms.field` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/TextBoxInteraction.cs` under the `forms.textbox` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Forms/TextareaInteraction.cs` under the `forms.textarea` interaction case. Status: completed.
- Strengthen Docs static tests so these Forms companion C# files remain registered, copied, and content-checked. Status: completed.

Follow-up audit queue:

- Continue companion C# coverage for Feedback loading surfaces (`EmptyState`, `Spinner`, `Progress`, `Skeleton`), Navigation simple disclosure/support primitives (`SegmentedControl`, `Accordion`, `Collapsible`, `Separator`, `Kbd`), Primitives (`Typography`, `FocusRing`, `Direction`), and Tokens Motion.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C179: Feedback Loading Companion CSharp Source Blocks

Status: completed for this Feedback loading companion-source pass. This pass closes the hidden C# source gap for `EmptyState`, `Spinner`, `Progress`, and `Skeleton` interaction examples so their action events, loading guards, reduced-motion settings, and animation toggles are visible under each local `Show code` block.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Feedback/EmptyStateInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Feedback/SpinnerInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Feedback/ProgressInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Feedback/SkeletonInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- Current-source audit showed `feedback.empty-state`, `feedback.spinner`, `feedback.progress`, and `feedback.skeleton` still rendered interaction behavior from hidden Docs C# while their local inline source reveal showed only AXAML.
- A focused read-only Agent independently reported these four loading interaction cases as `MISSING` before implementation: no `Code("Feedback/...Interaction.cs", "CSharp/Feedback/...Interaction.cs")` registration and no corresponding C# sample files.
- The missing hidden behavior covered `ActionRequested`, `SecondaryActionRequested`, command `CanExecute` guards, loading/disabled suppression, spinner `IsActive` and `RotationDuration`, progress determinate value changes and `IndeterminateAnimationDuration`, and skeleton `IsAnimated`, `PulseDuration`, pulse opacity, shimmer opacity, and static reduced-motion fallback.
- Existing multi-source `DocsExampleCase.CodeSamples` support made this a companion-file and registration pass while preserving the user-required order: component preview first, local `Show code` / `Hide code`, then copyable code blocks.

Implementation targets:

- Add and wire `Examples/CSharp/Feedback/EmptyStateInteraction.cs` under the `feedback.empty-state` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Feedback/SpinnerInteraction.cs` under the `feedback.spinner` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Feedback/ProgressInteraction.cs` under the `feedback.progress` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Feedback/SkeletonInteraction.cs` under the `feedback.skeleton` interaction case. Status: completed.
- Strengthen Docs static tests so these Feedback loading companion C# files remain registered, copied, and content-checked. Status: completed.

Follow-up audit queue:

- Continue companion C# coverage for Navigation simple disclosure/support primitives (`SegmentedControl`, `Accordion`, `Collapsible`, `Separator`, `Kbd`), Primitives (`Typography`, `FocusRing`, `Direction`), and Tokens Motion.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C180: Navigation Simple Companion CSharp Source Blocks

Status: completed for this Navigation simple companion-source pass. This pass closes the hidden C# source gap for `SegmentedControl`, `Accordion`, `Collapsible`, `Separator`, and `Kbd` interaction examples so their source-aware events, programmatic state changes, disabled guards, and reduced-motion examples are visible under each rendered `Show code` block.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/SegmentedControlInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/AccordionInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/CollapsibleInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/SeparatorInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Navigation/KbdInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- Current-source audit showed these `Navigation/*Interaction.axaml` cases rendered behavior from hidden Docs C# while the local inline source reveal showed only AXAML.
- A focused read-only Agent independently reported all five interaction cases as `MISSING` before implementation: no `Code("Navigation/...Interaction.cs", "CSharp/Navigation/...Interaction.cs")` registration and no corresponding C# sample files.
- A post-implementation read-only Agent confirmed all five registrations and files exist, with only whitespace-sensitive `IsEnabled=false` checks reported as exact-string misses because the samples use normal C# formatting (`IsEnabled = false`).
- Existing multi-source `DocsExampleCase.CodeSamples` support made this a companion-file and registration pass while preserving the user-required order: component preview first, local `Show code` / `Hide code`, then copyable code blocks.

Implementation targets:

- Add and wire `Examples/CSharp/Navigation/SegmentedControlInteraction.cs` under the `navigation.segmented-control` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Navigation/AccordionInteraction.cs` under the `navigation.accordion` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Navigation/CollapsibleInteraction.cs` under the `navigation.collapsible` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Navigation/SeparatorInteraction.cs` under the `navigation.separator` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Navigation/KbdInteraction.cs` under the `navigation.kbd` interaction case. Status: completed.
- Strengthen Docs static tests so these Navigation companion C# files remain registered, copied, and content-checked. Status: completed.

Follow-up audit queue:

- Primitives (`Typography`, `FocusRing`, `Direction`) and Tokens Motion companion coverage is closed by C181; continue broader Web-parity audits for default style leakage, event source metadata, and animation fidelity.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C181: Primitives And Tokens Companion CSharp Source Blocks

Status: completed for this Primitives/Tokens companion-source pass. This pass closes the hidden C# source gap for `Typography`, `FocusRing`, `Direction`, and `Motion` interaction examples so role switching, focus-ring geometry, direction events, runtime motion helpers, and reduced-motion handoff are visible under each rendered `Show code` block.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Primitives/TypographyInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Primitives/FocusRingInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Primitives/DirectionInteraction.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/CSharp/Tokens/MotionInteraction.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- Current-source audit showed `primitives.typography`, `primitives.focus-ring`, `primitives.direction`, and `tokens.motion` rendered interaction behavior from hidden Docs C# while the local inline source reveal showed only AXAML.
- A focused read-only Agent independently reported all four interaction cases as `MISSING` before implementation: no `Code("Primitives/...Interaction.cs", "CSharp/Primitives/...Interaction.cs")` / `Code("Tokens/MotionInteraction.cs", "CSharp/Tokens/MotionInteraction.cs")` registration and no corresponding C# sample files.
- `Examples/CSharp/Primitives` previously contained only `OverlayInteraction.cs`; `Examples/CSharp/Tokens` did not exist before this pass.
- Existing multi-source `DocsExampleCase.CodeSamples` support made this a companion-file and registration pass while preserving the user-required order: component preview first, local `Show code` / `Hide code`, then copyable code blocks.

Implementation targets:

- Add and wire `Examples/CSharp/Primitives/TypographyInteraction.cs` under the `primitives.typography` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Primitives/FocusRingInteraction.cs` under the `primitives.focus-ring` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Primitives/DirectionInteraction.cs` under the `primitives.direction` interaction case. Status: completed.
- Add and wire `Examples/CSharp/Tokens/MotionInteraction.cs` under the `tokens.motion` interaction case. Status: completed.
- Strengthen Docs static tests so these Primitives/Tokens companion C# files remain registered, copied, and content-checked. Status: completed.

Follow-up audit queue:

- Continue broader Web-parity audits for default style leakage, event source metadata, animation fidelity, and missing component/chart coverage beyond this companion-code slice.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C182: AvatarGroup Independent Style Architecture

Status: completed for this component architecture and style-fidelity slice. This pass promotes `CodexAvatarGroup` from a top-level control whose styles were hidden inside `Avatar.axaml` into an independently included style file, matching the component-per-style-file architecture used by the rest of the library and making future default-style leakage guards cover AvatarGroup directly.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Avatar.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/AvatarGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- `ExpectedTopLevelControls` already listed `CodexAvatarGroup`, but the shared `Components` style-file guard did not include `AvatarGroup`, so `EveryComponentHasOwnStyleFileAndThemeInclude` could not catch missing AvatarGroup style registration.
- `CodexAvatarGroup` and `CodexAvatarGroupCount` styles lived inside `Themes/Controls/Avatar.axaml`, while `ComponentStyles.axaml` included only `Avatar.axaml`; this made AvatarGroup an architectural exception compared with other first-class components.
- `ControlStateTests` still mapped `CodexAvatarGroup` and `CodexAvatarGroupCount` size selector checks to the `Avatar` style file, proving the state matrix was coupled to the old hidden-style placement.
- A read-only style-leak Agent was launched with a broad default-style leakage audit prompt. It ran long and was stopped to avoid a dangling process; its partial output independently highlighted raw native-part risk areas, while this slice closed the local AvatarGroup style architecture gap found during the same audit pass.

Implementation targets:

- Move all `CodexAvatarGroup`, `CodexAvatarGroupCount`, `avatar-group-item`, size, disabled, count-surface, and transition selectors into a new `Themes/Controls/AvatarGroup.axaml`. Status: completed.
- Add `Themes/Controls/AvatarGroup.axaml` to `ComponentStyles.axaml` so AvatarGroup styles are loaded through the same component style registry as other controls. Status: completed.
- Add `AvatarGroup` to the shared component style-file guard so it must keep its own style file and theme include. Status: completed.
- Update size selector tests so `CodexAvatarGroup` and `CodexAvatarGroupCount` validate against `AvatarGroup.axaml` instead of `Avatar.axaml`. Status: completed.
- Add a focused style-fidelity test proving Avatar no longer contains AvatarGroup selectors and AvatarGroup owns count chrome, motion tokens, size selectors, disabled state, and no Fluent/BasedOn path. Status: completed.

Follow-up audit queue:

- DatePicker/Combobox trigger and clear button scoped chrome is closed by C183; continue the raw native-part style audit for overlay close buttons and scoped ScrollViewer templates.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~ComponentStructureTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C183: DatePicker And Combobox Raw Button Chrome Guards

Status: completed for this raw native-part style-fidelity slice. This pass closes the DatePicker/Combobox trigger and clear-button audit item by ensuring their embedded raw Avalonia `Button` parts are scoped, templated, token-animated, and state-guarded instead of relying on default Button chrome.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/DatePicker.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Combobox.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- Both `DatePicker.axaml` and `Combobox.axaml` intentionally use raw `Button` controls for `PART_Trigger` and `PART_Clear`, with custom `ControlTemplate TargetType="Button"` definitions to avoid default Fluent chrome.
- `Combobox.axaml` already had scoped base transitions for `Button#PART_Clear` and `Button#PART_Trigger`, while `DatePicker.axaml` did not, so the DatePicker trigger/clear buttons could regress without a dedicated button-level motion contract.
- Neither file previously had a shared test proving that raw trigger/clear Button parts stayed scoped, templated, transition-backed, and free of unscoped `Button#PART_Clear` / `Button#PART_Trigger` selectors.
- A focused read-only Agent was launched for DatePicker/Combobox raw-button leakage. It ran long and was stopped to avoid a dangling process; its partial output independently highlighted the same raw-button selector/state blind spot.

Implementation targets:

- Add scoped `CodexDatePicker /template/ Button#PART_Clear` and `Button#PART_Trigger` transitions using Codex motion tokens. Status: completed.
- Add scoped DatePicker `PART_ClearSurface` / `PART_TriggerSurface` brush transitions and icon transitions for `PART_CalendarIcon`, `PART_ClearIcon`, and `PART_Chevron`, including chevron transform motion. Status: completed.
- Add DatePicker clear-button pointerover and pressed surface states so the embedded clear action has Web-style hover/pressed feedback. Status: completed.
- Add scoped Combobox `PART_ClearSurface` / `PART_TriggerSurface` brush transitions, `PART_ClearIcon` foreground motion, and clear-button pointerover/pressed surface states. Status: completed.
- Add a focused static guard proving DatePicker and Combobox raw Button parts are templated, scoped to their owner component, motion-backed, and not using unscoped Button selectors or Fluent/BasedOn defaults. Status: completed.

Follow-up audit queue:

- Continue raw native-part style audits for overlay close buttons (`Dialog`, `Popover`, `Sheet`, `Drawer`, `CommandDialog`, `Toast`) and scoped ScrollViewer templates.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~ComponentStructureTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C184: Overlay Close Raw Button Chrome Guards

Status: completed for this overlay/feedback close-button style-fidelity slice. This pass closes the next raw native-part audit item by keeping overlay close buttons scoped to their owner components with Web-style hover, pressed, custom content, and tokenized motion instead of letting raw Avalonia `Button` chrome leak through.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Popover.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Toast.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/CommandDialog.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- A focused read-only Agent was launched for overlay/feedback close raw-button leakage across `Dialog`, `Popover`, `Sheet`, `Drawer`, `CommandDialog`, `Toast`, and `Sonner`. It confirmed the local audit direction before being stopped to avoid a dangling process.
- `Dialog`, `Sheet`, `Drawer`, and `CommandDialog` already had scoped `Button#PART_Close:pointerover` and `Button#PART_Close:pressed`; `Popover` and `Toast` only had pointerover, so pressed feedback was missing for two shadcn-style dismiss actions.
- `Popover` and `Toast` also lacked button-level close transitions, so hover/pressed feedback could regress without an owner-scoped motion contract.
- `CommandDialog` inherits Dialog close-content APIs but its custom close template did not name `PART_CloseIcon` / `PART_CloseContent` or bind inherited `CloseContent`, making custom close content inconsistent with other overlay surfaces.

Implementation targets:

- Add scoped Popover and Toast close-button transitions using `CodexSwitch.MotionDurationDefault` and `CodexSwitch.MotionEaseOut`. Status: completed.
- Add Popover and Toast `Button#PART_Close:pressed` opacity feedback to match Dialog, Sheet, Drawer, and CommandDialog. Status: completed.
- Align CommandDialog close template with the shared overlay anatomy by naming `PART_CloseIcon`, adding `PART_CloseContent`, binding inherited `CloseContent`, adding close-button transitions, and adding `has-close-content` icon/content swaps. Status: completed.
- Add a focused static guard proving Dialog, Popover, Sheet, Drawer, CommandDialog, and Toast close buttons are templated, owner-scoped, hover/pressed state-backed, motion-backed, close-content capable, and free of unscoped `Button#PART_Close` selectors or Fluent/BasedOn defaults. Status: completed.

Follow-up audit queue:

- Continue raw native-part style audits for scoped ScrollViewer templates and other embedded native parts, then return to Docs case-density work for components that still have too few independent AXAML examples.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~ComponentStructureTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C185: Dropdown List ScrollViewer Chrome Guards

Status: completed for this Forms popup-scroll style-fidelity slice. This pass starts the scoped ScrollViewer audit by moving the most visible dropdown list scrollbars in `Select`, `NativeSelect`, and `Combobox` off default Avalonia chrome and onto owner-scoped Web-style ScrollViewer, ScrollBar, Track, and Thumb templates.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Select.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/NativeSelect.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Combobox.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs/ui-web-parity-agent-plan.md`

Evidence gathered:

- `ScrollArea.axaml` and `Table.axaml` already own scoped `ScrollViewer`, `ScrollBar`, and `Thumb` templates, proving the local Web-style scrollbar pattern and motion contract.
- A focused read-only Agent was launched for embedded `ScrollViewer` / `ScrollBar` leakage. Its partial audit identified `Select`, `NativeSelect`, `Combobox`, `Command`, `Menubar`, `ContextMenu`, `Drawer`, and `ApplicationShell` as remaining raw ScrollViewer sites, while `ScrollArea` and `Table` already have custom templates.
- The dropdown list cluster is the highest-risk first slice because overflow popups expose visible scrollbars directly beside shadcn-style option rows; default platform scrollbars break hover, pressed, radius, opacity, and motion consistency even when item templates are correct.

Implementation targets:

- Name the Select and NativeSelect popup list scroll viewers `PART_Scroll`, matching Combobox's existing `PART_Scroll` part. Status: completed.
- Add owner-scoped `ScrollViewer#PART_Scroll` templates for Select, NativeSelect, and Combobox with `PART_ScrollRoot`, `PART_ContentPresenter`, horizontal/vertical scrollbars, corner surface, gesture recognizer, and transparent background. Status: completed.
- Add owner-scoped vertical and horizontal `ScrollBar` templates with hidden page buttons, track, thumb, and Web-style 10px rails. Status: completed.
- Add owner-scoped `Thumb#PART_Thumb` templates with `PART_ThumbSurface`, muted foreground color, rounded radius, hover foreground promotion, opacity feedback, and tokenized opacity/background transitions. Status: completed.
- Add a focused static guard proving the three dropdown list scroll viewers are named, templated, motion-backed, owner-scoped, and free of unscoped `ScrollViewer` / `ScrollBar` selectors or Fluent/BasedOn defaults. Status: completed.

Follow-up audit queue:

- Continue scoped ScrollViewer audits for Command list, Menubar/ContextMenu submenu surfaces, Drawer content scrolling, ApplicationShell navigation scrolling, and text-input scroll presenters.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~ComponentStructureTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` from `docs-site/`
- `npm run build` from `docs-site/`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C163: Item Activated Source Metadata

Status: completed for this Data Display event-parity pass. This slice makes `CodexItem.Activated` report whether activation came from programmatic calls, pointer release, or keyboard activation, matching the source-aware event model already used by Tabs, Breadcrumb, Command, Pagination, and popup controls.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexItem.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- `CodexItem` already routed programmatic, Enter/Space, and primary pointer release through a single guarded activation path, but `CodexItemActivatedEventArgs` exposed only `CommandParameter`.
- Nested action suppression and command guard semantics were already present, so this pass only needed source metadata rather than a larger row-behavior rewrite.
- Docs Item interaction examples already expose status text for activation feedback and the page already participates in the inline `Show code` / `Hide code` contract.

Implementation targets:

- Add `CodexItemActivationSource` with `Programmatic`, `Pointer`, and `Keyboard`. Status: completed.
- Extend `CodexItemActivatedEventArgs` with `Source` while keeping the existing `CommandParameter`. Status: completed.
- Route `TryActivate()`, `TryHandleActivationKey(...)`, and primary pointer release activation through the shared guard path with the correct source. Status: completed.
- Update Docs Item notes, event matrix, and live status text to show activation source metadata. Status: completed.
- Update docs-site Data Display pages in English and Chinese to document `Activated` source metadata. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`

### Agent C164: Provider Card Selected Source Metadata

Status: completed for this Data Display event-parity pass. This slice gives provider-row selection the same source-aware event shape as the adjacent Item, Command, Breadcrumb, Tabs, Pagination, and popup controls.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexProviderCard.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ProviderCardInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- A read-only Agent audit confirmed `CodexItem` was now source-aware and identified `CodexProviderCard` as the adjacent Data Display gap: active state changed, but consumers could not observe whether selection came from pointer, keyboard, or programmatic activation.
- Existing provider-card behavior already guarded selection through enabled, dragging, and command `CanExecute`, and already restricted pointer selection to left-button release.
- Because `CodexProviderCard` inherits `Button`, the implementation needed to preserve native `Click` / `Command` order while adding selection metadata after the selection guard passes.

Implementation targets:

- Add `CodexProviderCardSelectionSource` with `Programmatic`, `Pointer`, and `Keyboard`. Status: completed.
- Add `CodexProviderCardSelectedEventArgs` and `Selected` so consumers can observe source metadata plus the existing command parameter. Status: completed.
- Route `TrySelect()`, primary pointer release selection, and keyboard-triggered button clicks through source-aware selection without changing command guards. Status: completed.
- Update Docs Provider Card notes, event matrix, interaction preview status, and AXAML interaction copy to mention source metadata. Status: completed.
- Update docs-site Data Display pages in English and Chinese to document `Selected` source metadata. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ControlStateTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`

### Agent C165: Badge Activated Source Metadata

Status: completed for this Feedback event-parity pass. This slice gives interactive/link badges the same source-aware activation shape as Item, Provider Card, Breadcrumb, Command, and other Web-style trigger surfaces.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexBadge.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/BadgeInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/feedback.mdx`
- `docs-site/content/docs/zh/ui-system/feedback.mdx`

Evidence gathered:

- `CodexBadge` already had guarded activation, command `CanExecute` handling, keyboard activation, and primary pointer release filtering, but `CodexBadgeActivatedEventArgs` exposed only `CommandParameter`.
- Docs already described interactive/link badges as pointer and keyboard activatable, so the event args needed source metadata to match the Web-style trigger contract instead of only updating visible state.

Implementation targets:

- Add `CodexBadgeActivationSource` with `Programmatic`, `Pointer`, and `Keyboard`. Status: completed.
- Extend `CodexBadgeActivatedEventArgs` with `Source` while keeping `CommandParameter`. Status: completed.
- Route `TryActivate()`, `TryHandlePointerActivation(...)`, and Enter/Space through the same guard path with the correct source. Status: completed.
- Update Badge Docs notes, event matrix, interaction status text, and AXAML interaction copy to mention source-aware activation. Status: completed.
- Update docs-site Feedback pages in English and Chinese to document `Activated` source metadata. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`

### Agent C166: Calendar Anatomy Docs Case

Status: completed for this Docs coverage pass. This slice closes the `forms.calendar` gap identified by the read-only Docs audit: Calendar had states, composition, and interaction cases, but no independent anatomy example with its own AXAML source.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/CalendarAnatomy.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- Docs already use `BuildInlineExample` so every registered example renders before its local `Show code` / `Hide code` source block.
- The Calendar page had `CalendarStates.axaml`, `CalendarComposition.axaml`, and `CalendarInteraction.axaml`, but no `CalendarAnatomy.axaml`.
- Existing Calendar state/event matrices already name the anatomy-relevant states: outside days, week numbers, range edges, unavailable days, and active roving target.

Implementation targets:

- Add `CalendarAnatomy.axaml` as a standalone sample file. Status: completed.
- Register `Calendar anatomy` in `ExampleCasesFor("forms.calendar")` between states and composition. Status: completed.
- Add `BuildCalendarAnatomyPreview` covering single grid anatomy, range anatomy, week-number anatomy, and bounded day anatomy. Status: completed.
- Update Docs panel tests so the Calendar anatomy sample and preview builder are guarded by the inline-code case registry. Status: completed.
- Update docs-site Forms pages in English and Chinese to document Calendar's separate state/anatomy/composition/interaction coverage. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`

### Agent C167: Native Select Anatomy Docs Case

Status: completed for this Docs coverage pass. This slice closes the `forms.native-select` anatomy gap from the read-only Docs audit and tightens the structure tests so the example documents real template parts rather than only adding another state sample.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/NativeSelectAnatomy.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- `forms.native-select` had states, composition, and interaction examples, but no independent anatomy example.
- The Native Select template owns trigger, focus ring, placeholder, selected content host, chevron, popup, popup border, items presenter, option item root, option content, optgroup root, and optgroup label parts.
- A focused read-only Agent confirmed the 2x2 anatomy shape and recommended adding stronger template-part assertions alongside the Docs case.

Implementation targets:

- Add `NativeSelectAnatomy.axaml` as a standalone sample file. Status: completed.
- Register `Native Select anatomy` in `ExampleCasesFor("forms.native-select")` between states and composition. Status: completed.
- Add `BuildNativeSelectAnatomyPreview` covering trigger anatomy, open popup anatomy, disabled option anatomy, and invalid compact anatomy. Status: completed.
- Update Docs panel tests so the Native Select anatomy sample and preview builder are guarded by the inline-code case registry. Status: completed.
- Tighten `NativeSelectStyleOwnsOptionsGroupsInvalidAndOpeningMotion` with template-part assertions for the anatomy pieces. Status: completed.
- Update docs-site Forms pages in English and Chinese to document Native Select's separate state/anatomy/composition/interaction coverage. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`

### Agent C162: Popup OpenChanged Source Metadata

Status: completed for this popup/source-metadata slice. This pass makes the remaining dropdown and form popup controls expose Web-style `onOpenChange` source metadata, updates Docs interaction status text, and tightens the inline-code Docs contract that every rendered case shows its own `Show code` / `Hide code` block below the component.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDropdownButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSplitButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSelect.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNativeSelect.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCombobox.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- Previous overlay and calendar/date-picker slices already established source-aware `OpenChanged` as the desktop equivalent of Web `onOpenChange`.
- Dropdown button, split button, select, native select, and combobox still had open-change events that only exposed `IsOpen`, so Docs could not show pointer/keyboard/selection/input/clear origins.
- Docs already uses `BuildInlineExample` to render the component preview, then a local `Show code` / `Hide code` toggle and `DocsCodeBlock` for the exact AXAML sample. The remaining risk was semantic test drift rather than the runtime layout itself.

Implementation targets:

- Add `OpenChangeSource` enums and `Source` event args to DropdownButton, SplitButton, Select, NativeSelect, and Combobox. Status: completed.
- Route pointer, keyboard, programmatic, close-on-select, input, focus, clear, and item paths through source-aware open/close helpers. Status: completed.
- Fix Combobox clear behavior so clearing text suppresses the input auto-open source and reports `Clear` when the clear action opens the popup. Status: completed.
- Update Docs page notes, state/event matrices, and interaction previews so live `OpenChanged` status text includes `args.Source`. Status: completed.
- Expand Docs semantic coverage so additional multi-case navigation pages enter inline-code expansion tests and the `BuildInlineExample` child order is asserted as preview, then toggle, then code block. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~ComponentStructureTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`

### Agent C159: Checkbox Switch Toggle Source Metadata

Status: completed for this Web event parity slice. This pass extends the remaining standalone two-state form controls so their change events report whether the value came from host code, pointer input, or keyboard activation, matching the source-aware contracts already added to Select, Native Select, Toggle Group, Tabs, Radio Group, Slider, and navigation controls.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCheckBox.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSwitch.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexToggle.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/CheckboxInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SwitchInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ToggleInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Implementation targets:

- Add `CodexCheckBoxCheckedStateChangeSource`, `CodexSwitchCheckedChangeSource`, and `CodexTogglePressedChangeSource` with Programmatic, Pointer, and Keyboard values. Status: completed.
- Extend `CheckedStateChanged`, `CheckedChanged`, and `PressedChanged` event args with `Source` while keeping old/new value payloads and defaulting host changes to Programmatic. Status: completed.
- Route keyboard activation through explicit helpers so Checkbox Space, Switch Space/Enter, and Toggle Space/Enter publish Keyboard. Status: completed.
- Track primary pointer activation during the native toggle lifecycle and add source-aware internal setters for deterministic tests. Status: completed.
- Update Docs state/event matrices, live interaction status text, standalone AXAML samples, and docs-site Forms pages so Checkbox, Switch, and standalone Toggle explain source metadata while preserving per-example inline code expansion. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~CheckboxMirrorsWebCheckedStateChangeContract|FullyQualifiedName~ToggleAndToggleGroupMirrorWebPressedAndSelectionState|FullyQualifiedName~SwitchSyncsOptionalLabelContentState|FullyQualifiedName~ChoiceToggleAndRangeControlsOwnPartsEventsAndNativeChrome|FullyQualifiedName~DocsPagesRenderStateAndEventMatricesForWebParityContracts"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C160: Calendar And DatePicker Source Metadata

Status: completed for this Forms event-source parity slice. This pass extends the date-selection surfaces so Calendar and DatePicker events can distinguish host-driven updates from pointer and keyboard interaction, matching the source-aware contracts already added across the rest of the Forms selection controls.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCalendar.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDatePicker.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/CalendarInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/DatePickerInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn Calendar and Date Picker are state-driven selection surfaces where pointer day activation, keyboard roving/selection, and host-controlled value changes should be observable without collapsing into one undifferentiated event path.
- Nearby Codex Forms components now expose source-aware change events, including Checkbox, Switch, Toggle, ToggleGroup, Select, NativeSelect, RadioGroup, Slider, Tabs, and Combobox.
- Local Docs already satisfy the required per-case code reveal pattern through `BuildInlineExample`: the rendered example appears first, then that exact AXAML sample is expanded by the local `Show code` / `Hide code` button.

Implementation targets:

- Add `CodexCalendarChangeSource` and propagate it through selected date, range, display date, and active date event args. Status: completed.
- Add pending-source tracking in Calendar so public APIs default to Programmatic while day-button primary pointer release and keyboard navigation/selection publish Pointer and Keyboard. Status: completed.
- Add `CodexDatePickerChangeSource` and propagate it through selected date, range, and open-state event args. Status: completed.
- Route DatePicker trigger release, Escape/clear/open keyboard paths, and Calendar pointer sync through source-aware helpers while preserving public Programmatic defaults. Status: completed.
- Refresh Docs behavior notes, state/event matrices, live interaction status text, standalone AXAML interaction cases, and docs-site Forms pages to document source metadata while preserving per-example inline code expansion. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~CalendarSyncsSelectionRangeBookedBoundsAndClasses|FullyQualifiedName~CalendarDayButtonCommandBlocksSelectionBeforeActivation|FullyQualifiedName~DatePickerSyncsSelectionRangeOpenClearAndGuards|FullyQualifiedName~DatePickerTriggerPointerReleaseUsesPrimaryButtonOnly|FullyQualifiedName~DatePickerCalendarPointerReleaseSyncsOnlyPrimarySelection|FullyQualifiedName~DatePickerRangeCalendarPointerReleaseClosesOnlyWhenPrimarySelectionCompletes|FullyQualifiedName~DatePickerPointerReleaseContractsUsePrimaryButtonOnly|FullyQualifiedName~CalendarSelectionEventsExposeWebSources|FullyQualifiedName~DocsPagesRenderStateAndEventMatricesForWebParityContracts"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C161: Overlay OpenChanged Source Metadata

Status: completed for this Overlay and Disclosure event-source parity slice. This pass preserves the existing open boolean event shape while adding source metadata to overlay/disclosure open changes so hosts can distinguish pointer, keyboard, focus, hover, drag-dismiss, close-on-select, and programmatic paths.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexPopover.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCollapsible.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexAccordion.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexTooltip.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexHoverCard.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDrawer.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCommandDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- Dialog, Popover, Collapsible, Tooltip, and HoverCard already exposed `OpenChanged`, but the event args only carried `IsOpen`, which made primary pointer release, Enter/Space, Escape, outside pointer, focus-open, hover-open, and host-owned state indistinguishable.
- Dialog-derived surfaces such as AlertDialog, Sheet, Drawer, and CommandDialog inherit the same open-state contract, so fixing the base event source model gives those overlays the same Web parity surface.
- Local Docs already preserve the required per-example code reveal model through `BuildInlineExample`; this slice updates behavior notes and matrices without changing that render-first, expand-code-under-case flow.

Implementation targets:

- Add `CodexDialogOpenChangeSource` and propagate it through Dialog, AlertDialog, Sheet, Drawer, and CommandDialog open/close paths. Status: completed.
- Add `CodexPopoverOpenChangeSource` and route trigger pointer, trigger keyboard, Escape, outside pointer, and public open/dismiss APIs through source-aware helpers. Status: completed.
- Add `CodexCollapsibleOpenChangeSource`, route pointer and keyboard trigger paths, and let Accordion-owned item state updates preserve pointer/keyboard/programmatic source metadata for item-level `OpenChanged`. Status: completed.
- Add `CodexTooltipOpenChangeSource` and `CodexHoverCardOpenChangeSource` with Programmatic, Pointer, Focus, and Keyboard values, including delayed timer callbacks that preserve the original request source. Status: completed.
- Forward Drawer drag-dismiss as source=Pointer and CommandDialog close-on-select as the selected command item's source. Status: completed.
- Refresh Docs state/event matrices plus docs-site Overlay/Navigation pages to call out source-aware `OpenChanged`. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C146: Combobox And DatePicker Primary Pointer Trigger Parity

Status: completed for this Forms trigger event-mechanism slice. This pass extends the primary-pointer trigger architecture from overlay/dropdown controls into Combobox and DatePicker, removing popup trigger reliance on Avalonia Button `Click` while preserving their explicit keyboard input contracts.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCombobox.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDatePicker.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- `CodexCombobox` and `CodexDatePicker` still wired popup triggers through `_trigger.Click`, even though their keyboard paths already run through `TryHandleInputKey`.
- Button `Click` can conflate keyboard and pointer activation, while the Web parity direction now uses explicit primary pointer release guards across dropdowns, popovers, menus, items, badges, drawer handles, and date-picker calendar days.
- DatePicker already guarded calendar day synchronization through `TryHandleCalendarPointerRelease`, so the trigger path should use the same primary-button contract.

Implementation targets:

- Add `CodexCombobox.TryHandleTriggerPointerRelease` and route `PART_Trigger` pointer release through `PointerUpdateKind.LeftButtonReleased`. Status: completed.
- Add `CodexDatePicker.TryHandleTriggerPointerRelease` and route `PART_Trigger` pointer release through `PointerUpdateKind.LeftButtonReleased`. Status: completed.
- Remove trigger-open dependency on `_trigger.Click` for both controls while keeping clear buttons on command-style `Click`. Status: completed.
- Add behavior tests for right/middle suppression, primary release toggles, loading guards, and `OpenChanged` on both controls. Status: completed.
- Extend structure guards and Docs/docs-site copy for primary trigger release parity. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComboboxFiltersHighlightsSelectsClearsAndDismissesLikeWeb|FullyQualifiedName~ComboboxTriggerPointerReleaseUsesPrimaryButtonOnly|FullyQualifiedName~DatePickerSyncsSelectionRangeOpenClearAndGuards|FullyQualifiedName~DatePickerTriggerPointerReleaseUsesPrimaryButtonOnly|FullyQualifiedName~DatePickerPointerReleaseContractsUsePrimaryButtonOnly|FullyQualifiedName~ComboboxStyleOwnsSearchPopupItemStatesAndOpeningMotion|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C147: Docs Public Composition Primitive Coverage

Status: completed for this Docs example-coverage slice. This pass focuses on public component primitives that existed in the library but were only implicit in templates or broad examples, so users can now expand the local AXAML under each relevant case and copy the exact composition.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/Chart.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ItemAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/FieldGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/SidebarPrimitivesAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/CommandAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/MenubarComposition.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- Docs already render each example before its own `Show code` / `Hide code` AXAML block through `BuildInlineExample`.
- A scan of public controls versus AXAML sample usage showed several composable public primitives missing from copied examples: `CodexChart`, `CodexCommandShortcut`, `CodexFieldLegend`, `CodexItemHeader`, `CodexItemContent`, `CodexItemActions`, `CodexMenubarLabel`, and `CodexSidebarGroupContent`.

Implementation targets:

- Use `CodexChart` directly in the Chart default sample and C# preview while preserving the chart container slot contract. Status: completed.
- Add direct Command shortcut, Field legend, Item header/content/actions, Menubar label, and Sidebar group content samples. Status: completed.
- Keep C# previews aligned with the AXAML examples so rendered cases and expanded source teach the same composition. Status: completed.
- Add a Docs layout test that guards direct AXAML and preview usage for these public composition primitives. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsAxamlSamplesExposePublicCompositionPrimitivesDirectly|FullyQualifiedName~ExampleAxamlFilesAreStandaloneCopiedSamples|FullyQualifiedName~EveryDocsPageExposesAtLeastFourInlineCodeExamples"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C154: CommandDialog ItemSelected Source Forwarding

Status: completed for this Overlay command-dialog event slice. This pass preserves the `CodexCommandItem` source metadata added in the Command component when the same item is hosted inside `CodexCommandDialog`, so command palette overlays no longer collapse pointer or keyboard selection into the default programmatic path.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCommand.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCommandDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/CommandDialogInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- `CodexCommandDialog` reused `CodexCommandItemSelectedEventArgs`, but it re-created the payload without forwarding `Source`, so pointer and keyboard selections surfaced as `Programmatic`.
- The dialog content can be hosted through a template/content boundary, so relying on a bubbling `Button.Click` handler on the dialog is weaker than letting `CodexCommandItem.TrySelect` notify the nearest `CodexCommandDialog` owner directly.
- Docs already render the CommandDialog interaction case before its local AXAML source reveal, so the Docs update only needed to show and describe forwarded source metadata.

Implementation targets:

- Add `CodexCommandDialog.NotifyItemSelected` and `FindOwner` so command items can publish selected item, value, and source metadata to the nearest dialog host. Status: completed.
- Update `CodexCommandItem.TrySelect` to notify both its owning `CodexCommand` and nearest `CodexCommandDialog` while preserving command execution, disabled/loading guards, and source-specific pointer/keyboard/programmatic selection. Status: completed.
- Update CommandDialog Docs notes, event matrix, interaction preview status text, local AXAML interaction sample, and docs-site Overlay pages. Status: completed.
- Add rendered behavior coverage proving CommandDialog forwards Pointer, Keyboard, and Programmatic sources, plus structure and Docs guards. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~CommandDialogMirrorsCommandAndDialogCloseSemantics|FullyQualifiedName~CommandDialogForwardsCommandItemSelectionSourceMetadata|FullyQualifiedName~CommandItemSelectionPublishesSourceMetadataAndPrimaryPointerRelease|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ControlStateTests|FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`

### Agent C153: Command ItemSelected Source Metadata

Status: completed for this Navigation command-event slice. This pass aligns `CodexCommandItem` selection with the same source-aware event model used by nearby Web-parity controls: primary pointer release, keyboard activation, and public programmatic selection are explicit in `ItemSelected`.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCommand.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/CommandInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- `CodexCommandItemSelectedEventArgs` exposed the item and value, but hosts could not tell whether selection came from pointer, keyboard, or a public `TrySelect()` call.
- `CodexCommandItem` still selected through generic `Button.OnClick`, so right and middle pointer releases were not explicitly rejected at the component-contract level.
- Docs already render each Command case above a local `Show code` / `Hide code` AXAML block, so the correct Docs work was to enrich the interaction case and event/state matrices without changing the inline code expansion model.

Implementation targets:

- Add `CodexCommandItemSelectSource` and expose `Source` from `CodexCommandItemSelectedEventArgs`. Status: completed.
- Route primary pointer release through source=Pointer, generic click/keyboard activation through source=Keyboard, and public `TrySelect()` through source=Programmatic. Status: completed.
- Preserve `Command.CanExecute`, loading suppression, disabled suppression, command execution, sibling active selection, and selected item publication. Status: completed.
- Update Docs behavior notes, state/event matrices, Command interaction preview, local AXAML sample, and docs-site Navigation pages. Status: completed.
- Add behavior and source-structure coverage for pointer-only activation, keyboard/programmatic source metadata, and guard preservation. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~CommandFiltersKeyboardNavigatesAndPublishesSelection|FullyQualifiedName~CommandItemPointerActivationUsesPrimaryReleaseAndSourceMetadata|FullyQualifiedName~LoadingCommandSuppressesItemActivationAndCommandExecution|FullyQualifiedName~CommandItemSelectionPublishesSourceMetadataAndPrimaryPointerRelease|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ControlStateTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C152: Breadcrumb Link Activation Source Metadata

Status: completed for this Navigation routing-event slice. This pass aligns Breadcrumb ancestor-link activation with the same source-aware event model now used across selection and route surfaces: primary pointer release, keyboard activation, and programmatic activation are explicit in `LinkActivated`.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexBreadcrumb.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/BreadcrumbInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- `CodexBreadcrumbLinkActivatedEventArgs` already exposed link, item, index, href, and content metadata, but not whether the route came from pointer, keyboard, or host/programmatic activation.
- `CodexBreadcrumbLink` still selected through generic `Button.OnClick`, so right/middle pointer releases could not be explicitly rejected at the component-contract level.
- Nearby navigation surfaces now use primary pointer release helpers and source metadata, so Breadcrumb should not remain the odd route surface out.

Implementation targets:

- Add `CodexBreadcrumbLinkActivationSource` and expose `Source` from `CodexBreadcrumbLinkActivatedEventArgs`. Status: completed.
- Route `CodexBreadcrumbLink` primary pointer release through source=Pointer, generic keyboard activation through source=Keyboard, and public `TryActivate()` through source=Programmatic. Status: completed.
- Preserve `Command.CanExecute`, current-page suppression, disabled suppression, command execution, and normal Button click semantics. Status: completed.
- Update Docs, AXAML interaction sample, and docs-site Navigation pages to name source-aware Breadcrumb activation. Status: completed.
- Add behavior and source-structure coverage for pointer-only activation, keyboard/programmatic source metadata, and guard preservation. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~BreadcrumbCompositionMirrorsWebPathLinksAndCurrentPageGuard|FullyQualifiedName~BreadcrumbLinkActivationUsesPrimaryReleaseAndSourceMetadata|FullyQualifiedName~BreadcrumbLinkActivationHonorsCommandCanExecuteBeforePublishing|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`

### Agent C151: Navigation Selection Source Metadata Parity

Status: completed for this Navigation event-path slice. This pass aligns SideNav and SegmentedControl selection events with the source-aware Web parity contract already used by RadioGroup: primary pointer release, keyboard activation, and programmatic selection now remain distinguishable.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNavigationPrimitives.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SideNavInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SegmentedControlInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- `CodexSideNavValueChangedEventArgs` and `CodexSegmentedControlValueChangedEventArgs` exposed old/new item, index, and value metadata, but did not expose whether the change came from pointer, keyboard, or programmatic selection.
- Both item types still used generic `Button.OnClick` selection, so right/middle pointer release could not be explicitly separated from primary pointer release in tests.
- RadioGroup, ProviderCard, menu links, and other Web-parity surfaces now use explicit primary pointer release helpers, so navigation selection should follow the same event architecture.

Implementation targets:

- Add `CodexSideNavValueChangeSource` and `CodexSegmentedControlValueChangeSource`, and expose `Source` from both `ValueChanged` event payloads. Status: completed.
- Route `CodexSideNavItem` primary pointer release through source=Pointer and generic keyboard activation through source=Keyboard, while keeping command-blocked and disabled guards. Status: completed.
- Route `CodexSegmentedButton` primary pointer release through source=Pointer and keyboard activation through source=Keyboard, while keeping command-backed segments controlled. Status: completed.
- Update Docs, AXAML interaction samples, and docs-site Navigation pages to name primary pointer release and source metadata. Status: completed.
- Add behavior and source-structure coverage for pointer-only activation and source metadata. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~SideNavPublishesWebStyleValueChangedOnSelection|FullyQualifiedName~SideNavCommandCanExecuteSuppressesSelectionAndSyncsClasses|FullyQualifiedName~SideNavItemPointerActivationUsesPrimaryReleaseOnly|FullyQualifiedName~SegmentedControlPublishesWebStyleValueChangedOnSelection|FullyQualifiedName~SegmentedButtonsWithCommandsUseControlledSelection|FullyQualifiedName~SegmentedButtonPointerActivationUsesPrimaryReleaseOnly|FullyQualifiedName~NavigationPrimitivesOwnSelectionDisclosureAndSeparatorChrome"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ControlStateTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C150: RadioGroup Pointer And Source Metadata Parity

Status: completed for this Forms Radio Group event-path slice. This pass makes RadioGroup item activation report the same kind of source evidence that other Web-parity controls now expose: primary pointer release, Space/Enter activation, and roving arrow selection are distinct paths in `ValueChanged`.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexRadioGroup.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/RadioGroupInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- `CodexRadioGroupItem` still selected through generic `OnClick`, while the page contract described pointer and keyboard selection as equivalent paths.
- `CodexRadioGroupValueChangedEventArgs` already exposed old/new item, index, and value metadata, but not the activation source needed to verify pointer, keyboard, and roving focus parity.
- Existing Forms controls now prefer explicit primary pointer release helpers for trigger/selection surfaces, so RadioGroup should follow the same event architecture.

Implementation targets:

- Add `CodexRadioGroupValueChangeSource` and expose `Source` from `CodexRadioGroupValueChangedEventArgs`. Status: completed.
- Route roving arrow selection through `KeyboardNavigation`, Space/Enter selection through `Keyboard`, and item pointer selection through primary left-button release with `Pointer`. Status: completed.
- Add `TryHandlePointerActivation(PointerUpdateKind updateKind)` to RadioGroup items and reject right/middle pointer releases, disabled items, and loading roots. Status: completed.
- Update Docs and docs-site copy so the visible Radio Group contract names primary pointer release and source metadata. Status: completed.
- Add behavior and source-structure coverage for pointer-only activation and source metadata. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~RadioGroupMirrorsWebValueOrientationAndRovingSelectionState|FullyQualifiedName~RadioGroupItemPointerActivationUsesPrimaryReleaseOnly|FullyQualifiedName~RadioGroupItemPointerAndKeyboardContractsExposeWebSources"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C149: ProviderCard Primary Pointer Activation Parity

Status: completed for this ProviderCard event-path slice. This pass makes provider-row selection explicit about pointer source: left-button pointer release owns pointer selection, while keyboard and command behavior continue through the existing Button activation path.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexProviderCard.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ProviderCardInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- `CodexProviderCard` is a provider-row selection surface, but still selected through generic `Button.OnClick`.
- Existing ProviderCard parity already blocked disabled, dragging, and `Command.CanExecute=false` rows; the remaining gap was primary-pointer specificity.
- Nearby row-like surfaces already expose `TryHandlePointerActivation` / `PointerUpdateKind.LeftButtonReleased` guards, so ProviderCard should use the same event architecture.

Implementation targets:

- Add `TryHandlePointerActivation(PointerUpdateKind updateKind)` and accept only `LeftButtonReleased`. Status: completed.
- Route `OnPointerPressed` / `OnPointerReleased` through a primary press-start plus in-bounds release guard, then avoid duplicate row selection when Button `OnClick` is raised from the same pointer release. Status: completed.
- Keep keyboard and command activation behavior through `OnClick`, including disabled, dragging, and command-blocked guards. Status: completed.
- Add behavior and structure tests for right/middle suppression, primary release selection, and guard preservation. Status: completed.
- Update Docs/docs-site wording so the visible contract says primary pointer release, not generic click. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ProviderCardPointerActivationUsesPrimaryReleaseAndSelectionGuards|FullyQualifiedName~ProviderCardDraggingAndDisabledStatesSuppressSelectionAndCommand|FullyQualifiedName~ProviderCardCommandCanExecuteSuppressesSelectionAndSyncsClasses|FullyQualifiedName~CommandItemsAndProviderCardsSelectExclusivelyOnClick|FullyQualifiedName~DataDisplayTablePaginationAndChartPrimitivesOwnChromeAndMotion"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C148: Pagination Action Primary Pointer Parity

Status: completed for this Data Display Pagination action-event slice. This pass removes first/previous/next/last action navigation from generic Avalonia `Click` subscriptions and gives those button-like surfaces the same explicit Web-style split used elsewhere: primary pointer release for pointer activation, Enter/Space for focused action buttons, and existing root navigation keys for Home/End/Left/Right/PageUp/PageDown.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexPagination.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/PaginationInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- `CodexPagination` still attached `_firstButton.Click`, `_previousButton.Click`, `_nextButton.Click`, and `_lastButton.Click`, while surrounding overlay, dropdown, combobox, date-picker, item, and carousel work now uses explicit primary pointer release guards.
- Pagination page items already had command/loading/current/ellipsis guards; the missing event-path parity was specifically the boundary/action button surface.
- Docs and docs-site described generic action/click behavior, so the visible contract needed to name primary pointer release and Enter/Space action activation.

Implementation targets:

- Add `TryHandleActionPointerRelease(PointerUpdateKind updateKind, CodexPaginationPageChangeSource source)` and route action buttons through `InputElement.PointerReleasedEvent`. Status: completed.
- Add `TryHandleActionKey(Key key, CodexPaginationPageChangeSource source)` so focused action buttons preserve Enter/Space activation without relying on generic `Click`. Status: completed.
- Remove first/previous/next/last `Click += On...Clicked` subscriptions while preserving Home/End/Left/Right/PageUp/PageDown root navigation. Status: completed.
- Add behavior coverage for right/middle suppression, primary action source metadata, boundary/loading guards, and Enter/Space-only action keys. Status: completed.
- Extend structure guards and Docs/docs-site copy for the primary pointer action contract. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~PaginationMirrorsWebPageSelectionAndBoundaryNavigation|FullyQualifiedName~PaginationActionPointerReleaseUsesPrimaryButtonOnly|FullyQualifiedName~PaginationActionKeyboardActivationUsesEnterAndSpaceOnly|FullyQualifiedName~PaginationPageButtonUsesCommandAndLoadingGuardsBeforeActivation|FullyQualifiedName~DataDisplayTablePaginationAndChartPrimitivesOwnChromeAndMotion|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`

### Agent C145: Dropdown And Split Primary Pointer Trigger Parity

Status: completed for this dropdown trigger event-mechanism slice. This pass separates pointer and keyboard trigger paths for DropdownButton and SplitButton so menu opening no longer depends on Avalonia Button `Click` for trigger toggling; primary pointer release owns pointer toggles, while Enter/Space/ArrowDown own keyboard opening.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDropdownButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSplitButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- The previous keyboard parity slice made Enter, Space, and ArrowDown explicit, but the trigger template still used Button `Click` for pointer toggles.
- Button `Click` can conflate keyboard and pointer activation, which is exactly the class of event-trigger drift the Web parity goal calls out.
- Dialog, Popover, Sheet, Drawer, Menu, ContextMenu, Item, Badge, and other updated controls already use primary pointer release guards, so DropdownButton and SplitButton should follow the same event architecture.

Implementation targets:

- Add `CodexDropdownButton.TryHandleTriggerPointerRelease` and route trigger pointer release through `PointerUpdateKind.LeftButtonReleased`. Status: completed.
- Add `CodexSplitButton.TryHandleMenuTriggerPointerRelease` and route the menu trigger pointer release through `PointerUpdateKind.LeftButtonReleased`. Status: completed.
- Remove trigger-open dependency on `_trigger.Click` / `_menuTrigger.Click` while preserving SplitButton primary action `Click`. Status: completed.
- Extend behavior tests for right/middle suppression, primary release toggles, loading/content guards, keyboard paths, and structure guards. Status: completed.
- Update Docs event text and docs-site EN/ZH summaries to describe primary pointer release plus keyboard trigger parity. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DropdownButtonTriggerKeysOpenLikeWebMenuTrigger|FullyQualifiedName~SplitButtonMenuTriggerKeysOpenLikeWebMenuTrigger|FullyQualifiedName~DropdownButtonMirrorsDropdownMenuOpenDismissAndSelectSemantics|FullyQualifiedName~SplitButtonSeparatesPrimaryActionFromDropdownSemantics|FullyQualifiedName~ComponentStructureTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C144: Dropdown And Split Trigger Keyboard Parity

Status: completed for this dropdown trigger event-parity slice. This pass makes DropdownButton and SplitButton menu triggers explicitly open through Enter, Space, or ArrowDown, matching the Web dropdown trigger keyboard contract instead of relying on button click behavior for Enter/Space.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDropdownButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSplitButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- Web dropdown triggers open from Enter, Space, and ArrowDown, while the local trigger key helpers only handled ArrowDown directly.
- DropdownButton and SplitButton already share open, loading, content, close-on-select, Escape, and focus-return contracts, so the missing piece was explicit keyboard trigger parity.
- Docs already render each dropdown and split-button case above its local `Show code` / `Hide code` AXAML block, so only the event matrix and docs-site summaries needed wording updates.

Implementation targets:

- Update `CodexDropdownButton.TryHandleTriggerKey` to accept Enter, Space, and Down through one open path. Status: completed.
- Update `CodexSplitButton.TryHandleMenuTriggerKey` to accept Enter, Space, and Down through one open path. Status: completed.
- Add behavior tests for Enter, Space, Down, ignored keys, loading guards, `OpenChanged`, and Escape dismissal on both controls. Status: completed.
- Update structure guards and Docs event text to preserve the explicit Web trigger key contract. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DropdownButtonTriggerKeysOpenLikeWebMenuTrigger|FullyQualifiedName~SplitButtonMenuTriggerKeysOpenLikeWebMenuTrigger|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C143: Rendered Submenu And Table Alignment Guards

Status: completed for this rendered regression coverage slice. This pass turns the existing visual-pass hook into concrete headless coverage for two Web-parity surfaces that are easy to regress visually: submenu popups must render while open, and table head/cell alignment must place text at the expected left, center, and right positions after the template is applied.

Scope:

- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MenuRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/TableRenderedLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`

Evidence gathered:

- `NavigationDataComponentTests` still carried a visual-pass hook for submenu popups and table column alignment.
- Menu and ContextMenu already had behavioral tests for submenu open/close delay and keyboard routing, but the delayed pointer test captured a frame after closing rather than while both submenu surfaces were open.
- Table head and cell alignment already exposed `align-left`, `align-center`, and `align-right` classes and set `HorizontalContentAlignment`, but no rendered test proved the template placed text at the expected column positions.

Implementation targets:

- Assert open Menu and ContextMenu submenu surfaces are visible through `PART_Popup` / `PART_SubMenuSurface` and capture a rendered frame before closing. Status: completed.
- Add `TableRenderedLayoutTests` with headless left/center/right header and body cell alignment checks using actual visual bounds. Status: completed.
- Extend structure guards so rendered submenu and table alignment coverage stays present. Status: completed.
- Replace the stale visual-pass hook comment with the new coverage note. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~TableRenderedLayoutTests|FullyQualifiedName~MenuRenderedLifecycleTests|FullyQualifiedName~ComponentStructureTests"`

### Agent C142: Carousel Anatomy Docs Case

Status: completed for this Data Display Carousel Docs coverage slice. This pass replaces the implicit use of the Carousel composition case as anatomy coverage with a dedicated anatomy example, while preserving the Docs rule that the rendered component appears first and the local AXAML source expands directly under that case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/CarouselAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- The Carousel page already had default, states, composition, and interaction examples, but no standalone `CarouselAnatomy.axaml`.
- `DocsPanelLayoutTests` was listing `DataDisplay/CarouselComposition.axaml` in the anatomy sample bucket, which made the coverage look complete while the page lacked an anatomy-named source file.
- The existing Docs shell already handles local `Show code` / `Hide code` expansion for any registered `DocsExampleCase`, so adding a separate AXAML file and preview builder preserves the current architecture.

Implementation targets:

- Add `CarouselAnatomy.axaml` covering template-owned viewport, items, previous/next controls, status, and selected-index boundary classes. Status: completed.
- Register `BuildCarouselAnatomyPreview` under `data.carousel` before composition and reuse existing Carousel preview helpers. Status: completed.
- Extend Docs layout tests so Carousel anatomy is an explicit file and builder, while keeping Carousel composition as a separate case. Status: completed.
- Update docs-site EN/ZH Data Display notes to mention anatomy/state/composition/interaction Carousel cases. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C141: Carousel Boundary Action State Parity

Status: completed for this Data Display Carousel boundary-action parity slice. This pass makes carousel edge availability deterministic and inspectable before commands run, matching the Web carousel contract where previous/next controls expose disabled boundary state while looped carousels can still wrap from either edge.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCarousel.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Carousel.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/CarouselInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- `CodexCarousel` already exposed `CanGoPrevious`, `CanGoNext`, `can-previous`, and `can-next`, but `at-start` / `at-end` were written from the ScrollViewer offset in a delayed render callback.
- The delayed ScrollViewer-derived classes could drift from the selected slide state and were not available as deterministic state before previous/next commands were evaluated.
- The Docs interaction case already covered command, keyboard, loop, and vertical paths with inline AXAML reveal, so the missing piece was explicit boundary class visibility.

Implementation targets:

- Add `at-start`, `at-end`, `previous-disabled`, and `next-disabled` classes from `SelectedIndex`, `SlideCount`, `CanGoPrevious`, and `CanGoNext`. Status: completed.
- Stop mutating boundary classes from ScrollViewer offset after `BringIntoView`, leaving scroll positioning visual and class state selected-index driven. Status: completed.
- Add scoped previous/next button styles for boundary-disabled states using the shared disabled opacity token and arrow cursor. Status: completed.
- Update the Carousel interaction sample, state/event matrices, docs-site EN/ZH notes, and structure/navigation tests for deterministic boundary classes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C140: EmptyState Action Command Blocked State Parity

Status: completed for this Feedback EmptyState interaction parity slice. This pass makes command-backed primary and secondary empty-state actions expose visible Web-style blocked states before request events fire, while keeping the empty surface mounted and visually scoped to the affected action buttons.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexEmptyState.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/EmptyState.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/EmptyStateInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/feedback.mdx`
- `docs-site/content/docs/zh/ui-system/feedback.mdx`

Evidence gathered:

- `CodexEmptyState` already tracked `ActionCommand` and `SecondaryActionCommand` `CanExecuteChanged`, and used `CanExecuteAction` / `CanExecuteSecondaryAction` to suppress `ActionRequested` and `SecondaryActionRequested`.
- The root only exposed `can-action` and `can-secondary-action`; blocked host commands were not distinguishable from loading, disabled, or missing action states in styles or Docs.
- The interaction Docs case covered action requests, loading, disabled, and semantic variants, but not a real command-blocked action path under the local inline AXAML reveal.

Implementation targets:

- Add `action-command-blocked`, `secondary-action-command-blocked`, and aggregate `command-blocked` classes for enabled, non-loading action commands that cannot execute. Status: completed.
- Add scoped styles that dim only the blocked action button instead of muting the entire empty-state surface. Status: completed.
- Update the EmptyState interaction preview and AXAML sample with a real command-blocked action case. Status: completed.
- Extend Docs state/event matrices and docs-site EN/ZH Feedback notes with the command-blocked action contract. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C139: Item ActivateCommand Blocked State Parity

Status: completed for this Data Display Item interaction parity slice. This pass makes command-backed `CodexItem` rows expose the same Web-style blocked availability signal as the newer provider-card, pagination, calendar, segmented-control, and command-item slices: `CanExecute=false` is visible before activation and suppresses row events without replacing row slots.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexItem.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Item.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ItemInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- `CodexItem` already subscribed to `ActivateCommand.CanExecuteChanged` and used `CanActivate` to block `TryActivate()`, pointer release, and Enter/Space activation, but it did not expose an explicit `command-blocked` class for styles or Docs.
- `Item.axaml` had `can-activate`, loading, selected, and interactive hover/press selectors, but blocked command rows still looked hoverable because there was no disabled-like command state selector.
- The desktop Item interaction Docs case included a "Command blocked" row, but it used `IsLoading=true` rather than a real `CanExecute=false` command.

Implementation targets:

- Sync `command-blocked` on `CodexItem` when an interactive, enabled, non-loading command-backed row cannot activate. Status: completed.
- Add Item styles for blocked cursor, opacity, hover/focus reset, pressed transform reset, and selected blocked hover preservation. Status: completed.
- Update the Item interaction preview to attach a `CanExecute=false` command and keep the AXAML sample aligned through an explicit `command-blocked` row. Status: completed.
- Extend Docs state/event matrices and docs-site EN/ZH Data Display notes with Item command-blocked and `CanExecuteChanged` behavior. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C138: Command Item CanExecute State Parity

Status: completed for this Navigation Command interaction parity slice. This pass makes command-backed `CodexCommandItem` rows expose Web-style selectability before activation, so blocked commands do not become active results, do not emit `ItemSelected`, and do not run host commands.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCommand.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Command.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/CommandInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- `CodexCommandItem.CanSelect()` already considered `Command.CanExecute(CommandParameter)`, but the result was not exposed as a visible state class.
- `CodexCommand.TryHandleNavigationKey()` only skipped disabled rows, so a command-blocked row could remain in the active-result path even though selection was suppressed later.
- Recent SideNav, SegmentedControl, Pagination, and Calendar slices use explicit `can-select` / `can-activate` plus `command-blocked` classes before mutating component state.
- Desktop Docs already render the Command interaction case above its local `Show code` / `Hide code` AXAML expansion, so this slice only needed to enrich the case rather than change the Docs architecture.

Implementation targets:

- Add command subscription and `CanExecuteChanged` refresh to `CodexCommandItem`. Status: completed.
- Sync `can-select` and `command-blocked` classes from `CanSelect()` and `Command.CanExecute`. Status: completed.
- Refresh command item classes when the owning `CodexCommand.IsLoading` changes. Status: completed.
- Make active-result keyboard navigation and `TrySelectActiveItem()` skip non-selectable command items. Status: completed.
- Add command item styles for `can-select`, `command-blocked`, blocked hover, blocked focus-visible, and blocked active states. Status: completed.
- Extend Command Docs state/event matrices, the interaction AXAML sample, and docs-site Navigation notes with the command-blocked item contract. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C137: Calendar Day Command Guard And Docs Case

Status: completed for this Forms interaction parity slice. This pass closes the Calendar day-button activation gap so host commands can block a generated day cell before selection changes, matching the Web expectation that blocked/disabled day actions do not emit selection events.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCalendar.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Calendar.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/CalendarInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- `CodexCalendarDayButton` inherits `Button`, but previously selected its owner date before `base.OnClick()`, so a host command with `CanExecute=false` could not block the selection path.
- Recent navigation and pagination parity slices already expose `can-select` / `can-activate` and `command-blocked` classes before changing selection or page state.
- Desktop Docs already render each Calendar example above its local `Show code` / `Hide code` AXAML expansion, so the interaction sample only needed a command-blocked day case and state/event matrix coverage.

Implementation targets:

- Add `CodexCalendar.CanSelectDate(DateTime)` and root enabled-state day refresh so day cells can consider owner availability. Status: completed.
- Add `CodexCalendarDayButton.CanActivate`, command subscription, `CanExecuteChanged` class refresh, `can-activate`, and `command-blocked`. Status: completed.
- Change day activation order to run `base.OnClick()` only after the activation guard and before mutating Calendar selection. Status: completed.
- Add Calendar styles for `can-activate`, `command-blocked`, blocked hover, and blocked pressed states. Status: completed.
- Extend Calendar interaction Docs and docs-site Forms notes with the command-blocked day contract while preserving local inline code expansion. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `npm run lint` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C136: SegmentedControl Command Block State Parity

Status: completed for this Navigation SegmentedControl event-parity slice. This pass makes command-backed `CodexSegmentedButton` items expose `can-select` and `command-blocked` state before activation, while preserving the Web-style controlled-selection behavior where command-backed segments execute host commands without moving the indicator.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNavigationPrimitives.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ApplicationShell.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SegmentedControlInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- `CodexSegmentedControl` already owns root `SelectedValue`, `ValueChanged`, and measured indicator movement for Web `onValueChange` parity.
- `CodexSegmentedButton.OnClick()` intentionally returns after `base.OnClick()` when `Command` is present, keeping command-backed selection external to the component.
- That controlled path suppressed selection correctly when commands were present, but command availability was invisible to styles and Docs.
- Root `SelectButton()` and fallback sibling selection only checked `IsEnabled`, so helper-driven selection could still ignore command availability.

Implementation targets:

- Add `CanSelect` to `CodexSegmentedButton`, using `IsEnabled` and inherited `Command.CanExecute(CommandParameter)`. Status: completed.
- Gate root `SelectButton()`, fallback sibling selection, and click activation on `CanSelect`, while keeping command-backed segments externally controlled. Status: completed.
- Subscribe/unsubscribe to inherited `Command.CanExecuteChanged`, and sync `can-select` / `command-blocked` classes. Status: completed.
- Add command-blocked segmented-button styles that remove hover scale and press affordance without moving the selected indicator. Status: completed.
- Extend Docs state/event matrices, interaction AXAML, and docs-site Navigation notes for command-blocked segments. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ControlStateTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 77 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.

### Agent C135: Pagination Page Button Command Guard Parity

Status: completed for this Data Display Pagination event-parity slice. This pass reconnects `CodexPaginationPageButton` to the inherited `CodexButton` command/loading activation contract before requesting `PageChanged`, so page item commands can block page changes like Web pagination handlers.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexPagination.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Pagination.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/PaginationInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- `CodexPaginationPageButton` inherits `CodexButton`, but its `OnClick()` selected a page without calling `base.OnClick()`.
- That bypassed inherited command execution, `Command.CanExecute`, and `CodexButton.IsLoading` suppression for standalone or command-backed page items.
- `CodexPagination.SelectPage()` already suppresses root loading and boundary changes, so the missing piece was the page-item activation gate before command/click/page-change sequencing.
- Docs covered loading suppression and `PageChanged` source metadata, but not page-item command-blocked behavior.

Implementation targets:

- Add `CanSelectPageItem(int page)` on `CodexPagination` for page-item navigation guards. Status: completed.
- Add `CanActivate` to `CodexPaginationPageButton`, gating on enabled, not loading, not current, not ellipsis, command executability, and owning pagination availability. Status: completed.
- Subscribe/unsubscribe to inherited `Command.CanExecuteChanged`, sync `can-activate` / `command-blocked`, call `base.OnClick()` for command/click execution, then request `SelectPage(...PageItem)`. Status: completed.
- Add page-button command-blocked styling and tests proving command, loading, current, and root loading guards suppress activation. Status: completed.
- Extend Pagination Docs state/event matrices, interaction AXAML, and docs-site Data Display notes for page-item command guards. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 134 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.

### Agent C134: SideNav Command Block Selection Parity

Status: completed for this Navigation SideNav event-parity slice. This pass makes `CodexSideNavItem` honor inherited `Command.CanExecute` before changing the root `SelectedValue` or legacy sibling selection, so command-blocked rows no longer publish `ValueChanged` ahead of the host command path.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNavigationPrimitives.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ApplicationShell.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SideNavInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- `CodexSideNav` already owns Web-style `SelectedValue` and `ValueChanged` metadata for old/new item, index, and value.
- `CodexSideNavItem.OnClick()` called `base.OnClick()` and then selected through the root or fallback sibling path, so a blocked inherited command could still mutate navigation state.
- The root `SelectItem()` only checked `IsEnabled`, not command executability, so helper-driven selection had the same command-block gap.
- Docs covered disabled rows and root value changes, but not command-blocked navigation rows.

Implementation targets:

- Add `CanSelect` to `CodexSideNavItem` and gate click, root selection, and fallback sibling selection on `IsEnabled` plus `Command.CanExecute(CommandParameter)`. Status: completed.
- Subscribe/unsubscribe to inherited `Command.CanExecuteChanged`, and sync `can-select` / `command-blocked` classes on command, parameter, enabled, and selected state changes. Status: completed.
- Add SideNav command-blocked styling that suppresses hover/press affordance while preserving selected-row context when applicable. Status: completed.
- Add tests proving blocked commands suppress command execution, root `SelectedValue`, `ValueChanged`, and selected row changes, then recover after `CanExecuteChanged`. Status: completed.
- Extend Docs state/event matrices, interaction preview/source, and docs-site Navigation notes for command-blocked rows. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ControlStateTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 77 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.

### Agent C133: Sidebar Trigger And Rail Command Block Parity

Status: completed for this Layout sidebar event-parity slice. This pass makes `CodexSidebarTrigger` and `CodexSidebarRail` honor inherited `Command.CanExecute` before toggling provider/sidebar open state, matching the Web controlled-trigger expectation that blocked actions do not publish `OpenChanged`.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexApplicationShell.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ApplicationShell.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/index.mdx`
- `docs-site/content/docs/zh/ui-system/index.mdx`

Evidence gathered:

- `CodexSidebarTrigger.OnClick()` and `CodexSidebarRail.OnClick()` toggled the nearest provider/sidebar before calling `base.OnClick()`.
- A blocked inherited command could therefore suppress the host command while still changing sidebar open state and emitting `OpenChanged`.
- Trigger already used `CodexButton` loading suppression, but that loading guard did not include host command availability.
- Docs described trigger/rail/shortcut state sync, but not command-blocked trigger behavior.

Implementation targets:

- Add `CanToggle` to trigger and rail, gating toggles on `IsEnabled`, trigger `IsLoading`, and inherited `Command.CanExecute(CommandParameter)`. Status: completed.
- Subscribe/unsubscribe to trigger and rail `Command.CanExecuteChanged`, and sync `can-toggle` / `command-blocked` classes on command, parameter, enabled, loading, and sidebar state changes. Status: completed.
- Add command-blocked styles for trigger and rail so blocked controls keep state context while removing active press affordance. Status: completed.
- Add behavior tests proving blocked trigger/rail commands do not toggle or execute, then recover after `CanExecuteChanged`. Status: completed.
- Extend Docs state/event matrices and docs-site layout summary for command-blocked trigger/rail parity. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`: 97 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.

### Agent C132: ProviderCard Command Block Selection Parity

Status: completed for this ProviderCard event-parity slice. This pass makes provider rows honor inherited `Button.Command.CanExecute` before changing sibling active state, so command-blocked rows no longer select themselves or clear the current provider ahead of the host command path.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexProviderCard.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ApplicationShell.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ProviderCardInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- `CodexProviderCard` inherits `Button`, but its previous `CanSelect()` only checked `IsEnabled` and `IsDragging`.
- `OnClick()` called `base.OnClick()` and then selected sibling cards, so a blocked inherited command could suppress command execution while still changing active provider state.
- Legacy provider-row styling already treated dragging as a non-selection state, and newer navigation/link components already expose `can-activate` / `command-blocked` for Web-style command guards.
- Docs had selection, dragging, action-slot, and disabled examples, but not a command-blocked selection guard case.

Implementation targets:

- Extend `CanSelect()` to require `Command.CanExecute(CommandParameter)` when a command is present. Status: completed.
- Subscribe/unsubscribe to inherited `Command.CanExecuteChanged`, and sync `can-select` / `command-blocked` classes when command, parameter, enabled, or dragging state changes. Status: completed.
- Add ProviderCard command-blocked styling so hover/pressed feedback does not imply activation while active cards can retain their selected visual family. Status: completed.
- Add behavior and structure tests proving blocked commands suppress selection and command execution, then recover after `CanExecuteChanged`. Status: completed.
- Extend Docs state/event matrices and the ProviderCard interaction AXAML sample with a command-blocked row under the same local inline code expansion contract. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ControlStateTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 76 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.

### Agent C131: AlertDialog Part Command CanExecute Parity

Status: completed for this AlertDialog response-button command parity slice. This pass keeps the internal cancel/action part commands synchronized with host `CancelCommand` and `ActionCommand` `CanExecuteChanged`, so the visible response buttons update before activation just like Web dialog actions.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexAlertDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- `CodexAlertDialog` exposes host `CancelCommand` and `ActionCommand`, then wraps them with internal part commands bound to `PART_Cancel` and `PART_Action`.
- `CanCancel()` and `CanAction()` already consulted the host command `CanExecute`, but host `CanExecuteChanged` was not forwarded to the internal part commands.
- That left button availability stale when an async validation, destructive confirmation, or loading boundary flipped command executability after the dialog opened.
- Docs already described cancel/action response activation, but did not call out live `CanExecute` propagation for the response buttons.

Implementation targets:

- Resubscribe `CodexAlertDialog` to host cancel/action command changes and detach subscriptions when commands or the dialog visual tree change. Status: completed.
- Forward host `CanExecuteChanged` to the internal part commands through `RaisePartCommandStateChanged()`. Status: completed.
- Add tests proving cancel/action part commands raise availability changes, suppress execution while blocked, and execute again after host commands recover. Status: completed.
- Extend structure and Docs guards so the part-command forwarding contract and event matrix copy do not regress silently. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 81 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `git diff --check` from `/data/CodexSwitch` and `/data/CodexSwitch/CodexSwitchUI`: passed.

### Agent C130: Breadcrumb Link Command Block Parity

Status: completed for this Breadcrumb link parity slice. This pass makes breadcrumb links honor `Command.CanExecute` before publishing `LinkActivated`, so command-blocked links no longer route or emit activation metadata ahead of the native button command path.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexBreadcrumb.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Breadcrumb.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- `CodexBreadcrumbLink.OnClick` notified the owning breadcrumb before `base.OnClick()`, so a blocked inherited `Button.Command` could still publish `LinkActivated` even though Web-style link/button activation should be suppressed.
- `TryActivate()` only checked `IsCurrent` and `IsEnabled`, not the inherited `Command.CanExecute(CommandParameter)` state.
- Breadcrumb links did not expose synchronized `can-activate` or `command-blocked` classes, leaving command availability invisible to styles and Docs.
- Docs described current-page suppression but not command-blocked suppression.

Implementation targets:

- Add `CanActivate` to `CodexBreadcrumbLink` and gate both `TryActivate()` and `OnClick()` on `!IsCurrent`, `IsEnabled`, and `Command.CanExecute`. Status: completed.
- Subscribe/unsubscribe to inherited `Command.CanExecuteChanged`, sync `can-activate` and `command-blocked`, and update on command parameter/enabled/current changes. Status: completed.
- Add Breadcrumb style support for `command-blocked` with disabled opacity and non-click cursor. Status: completed.
- Extend behavior tests proving blocked breadcrumb commands suppress `TryActivate()`, command execution, and `LinkActivated`, then restore classes when executable again. Status: completed.
- Add structure and Docs guards for command-before-publish behavior and the visible event matrix copy. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 131 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `git diff --check` from `/data/CodexSwitch` and `/data/CodexSwitch/CodexSwitchUI`: passed.

### Agent C129: NavigationMenu Top-Level Link Command Block Parity

Status: completed for this NavigationMenu top-level link parity slice. This pass fixes command-blocked top-level link behavior so a failed `CanExecute` no longer falls through into viewport activation, and keeps link availability classes synchronized with host command state.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNavigationMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- `CodexNavigationMenuItem.TryHandlePointerRelease` and `TryHandleActivationKey` treated any failed `TryActivateLink()` as a content-trigger path, so a command-blocked top-level link could open the viewport instead of doing nothing.
- Top-level navigation-menu items did not subscribe to `Command.CanExecuteChanged`, leaving `can-activate` stale after host command state changed.
- Pointer focus still used `IsLeftButtonPressed` instead of the stricter `PointerUpdateKind.LeftButtonPressed` contract used by newer parity slices.
- Docs did not surface the command-blocked top-level link behavior.

Implementation targets:

- Add `CanActivateLink` and use it from `TryActivateLink`, `TryHandlePointerRelease`, and `TryHandleActivationKey`. Status: completed.
- Split link and content-trigger branches so command-blocked links return false without opening the viewport. Status: completed.
- Subscribe/unsubscribe top-level item commands to `CanExecuteChanged`, sync `can-activate` and `command-blocked`, and update on `IsEnabled` changes. Status: completed.
- Replace `IsLeftButtonPressed` pointer focus gating with `PointerUpdateKind.LeftButtonPressed`. Status: completed.
- Extend behavior tests for command-blocked top-level links, keyboard suppression, stale viewport prevention, and command-state classes. Status: completed.
- Add structure/Docs guards for `CanActivateLink`, command subscription, `command-blocked`, and command-blocked event matrix copy. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 130 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `git diff --check` from `/data/CodexSwitch` and `/data/CodexSwitch/CodexSwitchUI`: passed.

### Agent C128: NavigationMenu Content Link Primary Release Parity

Status: completed for this NavigationMenu content-link parity slice. This pass moves `CodexNavigationMenuLink` from inline pointer-release activation to the same testable primary-release helper pattern as other Web-aligned interactive components, and fixes command-state class synchronization for content links.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNavigationMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- `CodexNavigationMenuLink.OnPointerReleased` already checked `LeftButtonReleased`, but the gate was inline and not directly behavior-testable like top-level navigation items, menu leaves, badges, and items.
- Content links did not subscribe to `Command.CanExecuteChanged`, so `can-activate` stayed stale when the host command became blocked or available again.
- Docs described `Primary link` as a top-level link behavior only, leaving content-link pointer activation out of the visible event contract.

Implementation targets:

- Add `TryHandlePointerActivation(PointerUpdateKind updateKind)` to `CodexNavigationMenuLink` and route `OnPointerReleased` through it. Status: completed.
- Add `CanActivate`, `command-blocked`, and `CanExecuteChanged` subscription/unsubscription for NavigationMenu content links. Status: completed.
- Add behavior tests proving right/middle release do not activate content links, primary release activates, command blocking suppresses activation and updates classes, commandless links still emit `Activated`, and disabled links suppress activation. Status: completed.
- Add structure guards so content links cannot regress to inline `PointerUpdateKind == LeftButtonReleased && TryActivate()` checks or stale command state. Status: completed.
- Update NavigationMenu Docs page notes and event matrix so the primary-link contract covers content links too. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 130 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `git diff --check` from `/data/CodexSwitch` and `/data/CodexSwitch/CodexSwitchUI`: passed.

### Agent C127: Docs Manual Pointer Release Primary Contract

Status: completed for this Docs sample-code parity slice. This pass removes remaining bare `PointerReleased += (_, _)` handlers from Docs preview code so expanded examples no longer demonstrate right/middle release activation for rows or interactive card surfaces.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- `BuildTableInteractionPreview` selected rows from `PointerReleased += (_, _)`, so any pointer button release could update the selected row in the rendered sample and in the expanded source.
- `BuildCardInteractionPreview` updated the interactive card from a bare pointer release handler.
- `DataTablePaymentRow` used a bare pointer release handler for row activation in the DataTable sample.
- These were Docs-local examples rather than component internals, but the user specifically requires expanded case code to model the current component contract.

Implementation targets:

- Add `SelectRowFromPointer(PointerReleasedEventArgs args, CodexTableRow row, string model)` and gate table row selection on `PointerUpdateKind.LeftButtonReleased`. Status: completed.
- Gate interactive card and DataTable row sample handlers on `PointerUpdateKind.LeftButtonReleased` and mark accepted events handled. Status: completed.
- Add a Docs static guard that rejects bare `PointerReleased += (_, _)` handlers and verifies the primary release checks remain in the sample code. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests"`: 17 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `git diff --check` from `/data/CodexSwitch` and `/data/CodexSwitch/CodexSwitchUI`: passed.

### Agent C126: Badge Primary Pointer Activation Docs Parity

Status: completed for this Feedback Badge event/docs slice. This pass makes the interactive/link badge activation path explicit in code and Docs: primary pointer release, Enter, and Space share the badge command/Activated contract, while right and middle button release are ignored.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexBadge.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- `CodexBadge.OnPointerReleased` already checked for `LeftButtonReleased`, but the activation gate was inline and not directly testable like newer parity slices.
- `CodexBadge.OnPointerPressed` still used the broader `IsLeftButtonPressed` button-state check for pointer focus instead of the same `PointerUpdateKind` contract used elsewhere.
- The Badge Docs page had state/anatomy/interaction examples, but the event matrix did not expose the interactive/link badge primary pointer, keyboard, or `CanExecute` behavior.

Implementation targets:

- Add `TryHandlePointerActivation(PointerUpdateKind updateKind)` to centralize primary-release badge activation. Status: completed.
- Route `OnPointerReleased` through the helper and switch pointer focus gating to `PointerUpdateKind.LeftButtonPressed`. Status: completed.
- Add behavior tests proving right/middle release do not activate badges, primary release runs `Command` and `Activated`, and `CanExecute`/non-interactive states suppress activation. Status: completed.
- Add structure guards so Badge activation cannot regress back to inline release checks or button-state press checks. Status: completed.
- Update Badge Docs notes, interaction example copy, and event matrix with primary pointer, keyboard, `CanExecute`, and host status changes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 76 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `git diff --check` from `/data/CodexSwitch` and `/data/CodexSwitch/CodexSwitchUI`: passed.

### Agent C125: Drawer Handle Primary Pointer Drag Parity

Status: completed for this Drawer event-parity slice. This pass aligns `CodexDrawer` handle dragging with the Web/Vaul primary-pointer gesture contract: right and middle button press/release no longer begin or complete a handle drag, while programmatic drag APIs stay available for docs and host-driven examples.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDrawer.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- `CodexDrawer.OnHandlePointerPressed` only checked `IsLeftButtonPressed`, while `OnHandlePointerReleased` completed the drag for any captured release without checking `PointerUpdateKind`.
- This left the release half of the gesture less strict than Web pointer/click semantics and could let right or middle release complete a drag-dismiss flow.
- Docs described the Drawer gesture generically as `Handle drag` / `Drag release`, so the visible contract did not say the gesture is primary-pointer owned.
- Existing Docs inline examples already render the component first and reveal the matching local AXAML source under each case, so this slice only needed to refresh the Drawer gesture wording.

Implementation targets:

- Add `TryBeginHandleDrag(PointerUpdateKind updateKind, Point startPoint, IPointer? pointer = null)` and gate handle drag start on `PointerUpdateKind.LeftButtonPressed`. Status: completed.
- Add `TryCompleteHandleDrag(PointerUpdateKind updateKind, IPointer? pointer = null)` and gate handle drag completion on `PointerUpdateKind.LeftButtonReleased`. Status: completed.
- Keep public `BeginDrag`, `DragBy`, and `CompleteDrag` behavior intact for programmatic examples and tests. Status: completed.
- Update Drawer Docs page notes, example copy, and event matrix to call out `Primary handle drag` and `Primary drag release`. Status: completed.
- Add behavior tests proving right/middle press do not start drag, right/middle release do not complete drag, primary release completes dismissal, and disabled/closed/hidden-handle states suppress start. Status: completed.
- Add structure guards so Drawer handle drag cannot regress back to generic button state checks or unconditional release completion. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 74 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `git diff --check` from `/data/CodexSwitch` and `/data/CodexSwitch/CodexSwitchUI`: passed.

### Agent C124: NavigationMenu Top-Level Link Primary Release Parity

Status: completed for this Navigation event-parity slice. This pass aligns `CodexNavigationMenuItem` top-level link activation with the Web link/button contract: pointer press only handles pointer focus, while command activation waits for primary pointer release.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNavigationMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- `CodexNavigationMenuItem.OnPointerPressed` previously checked `IsLeftButtonPressed`, focused the item, then immediately called `TryActivateLink()` or `Activate()`.
- For top-level link items with no viewport content, that meant command execution and `Activated` fired on press-down rather than primary release/click completion.
- `CodexNavigationMenuLink` content links already use `PointerUpdateKind.LeftButtonReleased`, so the top-level item path was the remaining navigation-menu inconsistency.
- Docs described navigation-menu pointer behavior generically as pointer activation and did not distinguish pointer-enter viewport disclosure from primary-release link activation.

Implementation targets:

- Add `TryHandlePointerRelease(PointerUpdateKind updateKind, CodexNavigationMenu? owner = null)` to `CodexNavigationMenuItem`. Status: completed.
- Gate top-level pointer activation on `PointerUpdateKind.LeftButtonReleased` and `IsEnabled`. Status: completed.
- Keep `OnPointerPressed` limited to pointer focus and focus-visible suppression. Status: completed.
- Preserve pointer-enter viewport disclosure and keyboard Enter/Space activation. Status: completed.
- Update Navigation Menu Docs notes, example copy, and event matrix to call out pointer-enter viewport activation plus primary-release link activation. Status: completed.
- Add behavior tests proving right/middle release do not execute top-level link commands, primary release executes and closes the viewport, content items open on primary release, and disabled items suppress release handling. Status: completed.
- Add structure guards so top-level navigation-menu command activation cannot regress back to pointer press. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 125 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C123: Resizable Primary Pointer Drag Parity

Status: completed for this Layout event-parity slice. This pass aligns `CodexResizableHandle` drag activation with the Web resizable-panel contract: only the primary pointer can enter handle dragging, and only the primary pointer release can end that drag.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexResizable.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- `CodexResizableHandle.OnPointerPressed` previously called `owner.BeginResize(this)`, captured the pointer, and set dragging without checking `PointerUpdateKind`.
- `OnPointerReleased` ended resizing and cleared capture for every pointer release, so secondary or middle-button input could mutate the active dragging state.
- Docs described the Resizable interaction as generic pointer drag even though the Web interaction is primary pointer drag plus keyboard resize.

Implementation targets:

- Make `CodexResizablePanelGroup.BeginResize` and `EndResize` return whether the state transition ran. Status: completed.
- Add `TryBeginResize(PointerUpdateKind updateKind, Point startPoint, CodexResizablePanelGroup? owner = null)` and gate dragging on `PointerUpdateKind.LeftButtonPressed && IsEnabled`. Status: completed.
- Add `TryEndResize(PointerUpdateKind updateKind, CodexResizablePanelGroup? owner = null)` and gate drag completion on `PointerUpdateKind.LeftButtonReleased`. Status: completed.
- Keep keyboard resize unchanged through `TryHandleResizeKey`. Status: completed.
- Update Resizable Docs notes, example copy, state matrix, and event matrix from generic pointer drag to `Primary drag`. Status: completed.
- Add behavior tests proving right/middle pointer paths do not start or end dragging, primary release clears group/handle dragging, and disabled handles reject primary drag start. Status: completed.
- Add structure guards for the primary pointer helper path. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 123 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C122: Slider Primary Pointer Drag Commit Parity

Status: completed for this Forms event-parity slice. This pass aligns `CodexSlider` pointer dragging with the Web range-input contract: only the primary pointer can begin the drag state, and only the primary pointer release can commit `source=pointer`.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSlider.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- `CodexSlider.OnPointerPressed` previously set `_isPointerChanging = IsEnabled` and invoked the native Slider pointer path without checking `PointerUpdateKind`.
- That meant secondary or middle pointer presses could expose the `dragging` state and enter the value-change path, which does not match the Web primary-pointer range interaction.
- Docs already expose the Slider page as four inline expandable examples; the visible event contract needed to say `Primary drag` rather than generic pointer drag.

Implementation targets:

- Add `TryBeginPointerChange(PointerUpdateKind updateKind)` and gate dragging on `PointerUpdateKind.LeftButtonPressed && IsEnabled`. Status: completed.
- Add `TryCommitPointerValue(PointerUpdateKind updateKind)` and gate pointer commits on `PointerUpdateKind.LeftButtonReleased`. Status: completed.
- Keep focus loss cleanup through the existing pointer commit path so active drags cannot strand the `dragging` class. Status: completed.
- Update Slider Docs behavior notes, example copy, state matrix, and event matrix from generic pointer drag to primary pointer drag. Status: completed.
- Add behavior tests proving right/middle pointer paths do not start or commit dragging, primary release clears dragging and commits `source=pointer`, and disabled sliders reject primary drag start. Status: completed.
- Add structure guards for the primary pointer helper path. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 82 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C121: ProviderCard Drag Selection Suppression Parity

Status: completed for this Data Display event-parity slice. This pass aligns `CodexProviderCard` selection with the documented Web-style provider-row contract: primary click can select a provider, but dragging and disabled states suppress selection and command activation.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexProviderCard.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- `CodexProviderCard.OnClick` always called `SelectSiblingCards()` after `base.OnClick()`, so `IsDragging` was purely visual and could still select the dragged card if a click was raised.
- Docs already stated that drag state applies visual feedback without selecting another card, but the component code did not enforce that contract.
- Provider-card rows are Button-derived and may carry a command, so drag suppression needs to prevent both sibling active-state mutation and command activation.

Implementation targets:

- Add `TrySelect()` and `CanSelect()` to `CodexProviderCard`. Status: completed.
- Gate selection on `IsEnabled && !IsDragging`. Status: completed.
- Update `OnClick()` so dragging/disabled cards return before Button command/click execution and before sibling selection. Status: completed.
- Update the Docs `data.provider-card` event matrix from generic pointer release to `Primary pointer`, and clarify that drag state suppresses selection and command activation. Status: completed.
- Add behavior tests proving dragging and disabled cards do not select, do not clear the active sibling, and do not execute commands, while leaving normal `TrySelect()` selection intact. Status: completed.
- Add structure guards for the new selection gate. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ControlStateTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 67 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 246 passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C120: DatePicker Calendar Primary Pointer Sync Parity

Status: completed for this Forms event-parity slice. This pass tightens the `CodexDatePicker` embedded-calendar pointer path so the picker only syncs and close-on-selects after a primary calendar pointer release, matching Web date-picker day-button behavior.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDatePicker.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`

Evidence gathered:

- `CodexDatePicker` listened to `_calendar.PointerReleased` and called `SyncFromCalendar()` for every pointer release, without checking `PointerUpdateKind`.
- With an existing selected date or completed range, a secondary or middle-button release inside the calendar could close the popover through `CloseOnSelect` even though no Web-style primary day selection happened.
- Docs still described Calendar and Date Picker day selection as generic pointer day selection, while recent trigger and row passes have moved visible contracts to primary pointer wording.

Implementation targets:

- Add `TryHandleCalendarPointerRelease(PointerUpdateKind updateKind, CodexCalendar? calendar = null)` to `CodexDatePicker`. Status: completed.
- Gate calendar sync on `PointerUpdateKind.LeftButtonReleased`, `!IsLoading`, and `IsEnabled`. Status: completed.
- Make `SyncFromCalendar` return whether sync ran, and keep range close-on-select behavior only after a primary release completes the range. Status: completed.
- Update Docs event matrix labels for Calendar and Date Picker day selection to `Primary day`. Status: completed.
- Add behavior tests proving right/middle release do not sync or close, primary release syncs and closes single-date selection, loading blocks sync, and range mode closes only after a primary release completes the range. Status: completed.
- Add a structure guard so the embedded calendar pointer path keeps the primary-release and loading/disabled gate. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 81 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 245 passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C119: Item Row Primary Pointer Activation Parity

Status: completed for this Data Display event-parity slice. This pass aligns `CodexItem` row activation with the Web item/list-row contract: only primary pointer release activates an interactive row, secondary/middle release do not run commands, and nested interactive slots do not accidentally activate the parent row.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexItem.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- `CodexItem.OnPointerReleased` previously called `TryActivate()` after any unhandled pointer release without checking `PointerUpdateKind`.
- Docs described the row activation as generic pointer release, while recent overlay, disclosure, menubar, menu, and context-menu passes have moved visible contracts to primary pointer activation.
- Data Display rows often host nested actions such as buttons, badges, selects, sliders, or menus; Web-style row composition must let those child controls keep their own activation path without mutating the parent row.

Implementation targets:

- Add `TryHandlePointerActivation(PointerUpdateKind updateKind, object? source = null)` to `CodexItem`. Status: completed.
- Gate row activation on `PointerUpdateKind.LeftButtonReleased` plus the existing interactive/loading/disabled/command `CanExecute` guard. Status: completed.
- Expand nested activation suppression to inspect visual and logical ancestors for `Button`, `ToggleButton`, `TextBox`, `ComboBox`, `Slider`, `MenuItem`, and `CodexBadge` before activating the row. Status: completed.
- Update the Docs `data.item` event matrix from generic `Pointer released` to `Primary pointer`. Status: completed.
- Add behavior tests covering right, middle, primary, loading, and nested action source cases. Status: completed.
- Add structure guards so the primary-pointer gate and nested interactive source checks remain part of the Data Display row contract. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 121 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 242 passed.
- `git diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C118: Menu Leaf Primary Pointer Selection Parity

Status: completed for this event-parity pass. This slice tightens `CodexMenuItem` and `CodexContextMenuItem` so leaf selection mirrors the Web menu item contract: only primary pointer release enters the pointer-selection path, while right and middle release do not select, run commands, or close submenu chains.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexContextMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- Web menu item selection is primary-pointer activation, while secondary/middle pointer release should not trigger `onSelect` behavior.
- Existing Docs event matrices already moved overlay, disclosure, and menubar trigger wording to primary-trigger language; menu/context-menu leaf items still said generic pointer release.
- Current Avalonia overrides set the pending pointer selection source before calling `base.OnPointerReleased(e)` without checking `PointerUpdateKind`, leaving non-primary releases too close to the selection path.

Implementation targets:

- Add `TryHandlePointerSelection(PointerUpdateKind updateKind)` to `CodexMenuItem` and `CodexContextMenuItem`. Status: completed.
- Gate pointer selection on `PointerUpdateKind.LeftButtonReleased` plus the existing disabled/loading/command `CanExecute` activation guard. Status: completed.
- Preserve blocked activation handling by marking disabled/loading/command-blocked pointer release handled while ignoring non-primary releases. Status: completed.
- Update Docs event matrix wording for Menu and Context menu to `Primary pointer`. Status: completed.
- Add behavior tests for right, middle, left, and loading cases on both menu and context-menu leaf items, including pointer source metadata and close-on-select propagation. Status: completed.
- Add structure tests that reject unguarded pending pointer selection in both controls. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C109: Radio Switch Toggle Slider Anatomy Docs And Chrome Guard

Status: completed for this Forms Docs parity pass. This slice brings the remaining standalone choice/toggle/range form pages up to the four-case Docs pattern by adding anatomy cases under each component, while keeping the local `Show code` / `Hide code` AXAML expansion directly beneath the rendered case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/RadioAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SwitchAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ToggleAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SliderAnatomy.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- The Docs preview contract already renders each example before its own local inline source toggle, so each new anatomy sample needed an independent AXAML file and `DocsExampleCase` registration rather than a right-rail-only source view.
- `CodexRadio`, `CodexSwitch`, `CodexToggle`, and `CodexSlider` already own Web-style focus-visible suppression, checked/pressed/value events, tokenized motion, and component templates. The missing Docs coverage was anatomy-level demonstration of template parts and event/state surfaces.
- Default Avalonia/Fluent style drift remains a risk for native-derived controls, so this slice adds a focused guard over Radio, Switch, Toggle, and Slider template parts, event paths, tokenized motion, disabled opacity, and absence of `BasedOn`/Fluent references.

Implementation targets:

- Add `RadioAnatomy.axaml` and `BuildRadioAnatomyPreview` covering ring, dot, label target, group name, size, intent, checked, and disabled cases. Status: completed.
- Add `SwitchAnatomy.axaml` and `BuildSwitchAnatomyPreview` covering track, thumb, optional content, checked motion, size, intent, and disabled cases. Status: completed.
- Add `ToggleAnatomy.axaml` and `BuildToggleAnatomyPreview` covering root, content, focus ring, pressed classes, variants, icon size, and disabled guard. Status: completed.
- Add `SliderAnatomy.axaml` and `BuildSliderAnatomyPreview` covering slider root, track background, fill, invisible hit buttons, thumb, vertical orientation, boundary classes, and disabled guard. Status: completed.
- Extend Docs registration tests and component chrome/event guards. Status: completed.
- Refresh Docs visual fingerprints after expanded inline-code rendering changed the affected pages. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~FormComponentDetailTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

Coverage audit:

- `TOTAL_LT4 24`
- Forms pages with fewer than four cases from the previous audit (`forms.radio`, `forms.switch`, `forms.toggle`, and `forms.slider`) are now removed from the under-covered list.

### Agent C110: Navigation Anatomy Docs And Native Chrome Guard

Status: completed for this Navigation Docs parity pass. This slice brings the remaining under-covered Navigation pages up to the four-case Docs pattern by adding independent anatomy examples that still render their own `Show code` / `Hide code` AXAML directly beneath the component preview.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SideNavAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SegmentedControlAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/DropdownButtonAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/CollapsibleAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SeparatorAnatomy.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- `navigation.side-nav`, `navigation.segmented-control`, `navigation.dropdown`, `navigation.collapsible`, and `navigation.separator` each had default/state/interaction coverage but only three local cases.
- Existing controls already expose the Web-style contracts needed for these anatomy pages: side-nav root `SelectedValue`, segmented moving indicator metrics, dropdown light-dismiss/open/focus-return surface, collapsible measured height animation, and separator orientation/size classes.
- Native/default style drift is most likely around Button-derived navigation rows, dropdown popup trigger chrome, and templated disclosure controls, so this slice adds a guard that checks template parts, event paths, tokenized motion, and absence of Fluent/BasedOn references across the relevant styles.

Implementation targets:

- Add `SideNavAnatomy.axaml` and `BuildSideNavAnatomyPreview` for root selected value, item values, icon/detail slots, selected row, and disabled row. Status: completed.
- Add `SegmentedControlAnatomy.axaml` and `BuildSegmentedControlAnatomyPreview` for indicator host, selected segment metrics, value fallback, disabled segment, and remeasure motion. Status: completed.
- Add `DropdownButtonAnatomy.axaml` and `BuildDropdownAnatomyPreview` for trigger, chevron, popup, surface, arrow, content presenter, alignment, close-on-select, and loading guard. Status: completed.
- Add `CollapsibleAnatomy.axaml` and `BuildCollapsibleAnatomyPreview` for trigger, rich header slot, chevron, content clip, measured content, animation duration, and disabled closed state. Status: completed.
- Add `SeparatorAnatomy.axaml` and `BuildSeparatorAnatomyPreview` for `PART_Line`, horizontal/vertical orientation, density sizes, toolbar dividers, and decorative composition. Status: completed.
- Extend Docs registration tests and Navigation native chrome/style guards. Status: completed.
- Refresh Docs visual fingerprints after expanded inline-code rendering changed these Navigation pages. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~NavigationDataComponentTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

Coverage audit:

- `TOTAL_LT4 19`
- Navigation pages with fewer than four cases from the previous audit (`navigation.side-nav`, `navigation.segmented-control`, `navigation.dropdown`, `navigation.collapsible`, and `navigation.separator`) are now removed from the under-covered list.

### Agent C105: Feedback Loading Anatomy Docs And Style Leak Guard

Status: completed for this Docs coverage and fidelity slice. This pass focuses on feedback loading primitives where default Avalonia `ProgressBar` chrome or under-documented runtime animation behavior can drift from the Web/shadcn effect.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/SpinnerAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/ProgressAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/SkeletonAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- Docs already render each `DocsExampleCase` before its local `Show code` / `Hide code` button via `BuildInlineExample`, so adding standalone AXAML files automatically places the exact sample code below the rendered loading component case.
- Spinner, Progress, and Skeleton had default/state/interaction coverage, but no independent anatomy page showing template parts, runtime motion knobs, semantic tone, and reduced-motion fallback.
- `CodexProgress` inherits Avalonia `ProgressBar`, so it needs explicit guards for owned template, zero border/padding, null focus adorner, and Codex indeterminate segment to prevent Fluent/default chrome leakage.

Implementation targets:

- Add Spinner anatomy Docs case with spoke/stroke sizing, semantic foreground, live label, paused state, and non-interactive composition. Status: completed.
- Add Progress anatomy Docs case with track, indicator, progress text, semantic variants, large/small sizes, indeterminate segment, and zero-duration reduced-motion frame. Status: completed.
- Add Skeleton anatomy Docs case with text/media placeholders, pulse opacity, shimmer layer, rounded blocks, static fallback, and local inline code reveal. Status: completed.
- Register all three anatomy cases in `ExampleCasesFor` so each page now has a richer Default/States/Anatomy/Interaction sequence. Status: completed.
- Add tests covering the new Docs sample registration plus loading-component native style leak guards. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~MotionRenderedLifecycleTests|FullyQualifiedName~OverlayFeedbackComponentTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C106: Feedback Surface Anatomy Docs And Scoped Chrome Guard

Status: completed for this Docs coverage and style-fidelity slice. This pass completes the remaining Feedback pages that still had only default/state/interaction examples and adds a scoped chrome guard for empty-state action buttons and Sonner close-button policy.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/EmptyStateAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/SonnerAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- The current Docs shell already guarantees local inline source disclosure through `BuildInlineExample`, so adding independent AXAML files under each registered case keeps the code reveal directly beneath the rendered component.
- `feedback.empty-state` and `feedback.sonner` were the only remaining Feedback pages below four cases after C105; both lacked an anatomy example that shows slot composition and host chrome.
- `CodexEmptyState` owns `PART_Action` and `PART_SecondaryAction` through `CodexButton`, while `CodexSonner` scopes close-button visibility through `CodexToast /template/ Button#PART_Close`; these are the style fidelity points that can drift if native Button defaults leak in.

Implementation targets:

- Add Empty State anatomy Docs case with icon shell, header, helper text, content slot, primary action, secondary action, size, and semantic variant examples. Status: completed.
- Add Sonner anatomy Docs case with expanded rich viewport, compact close policy, animated host rows, visible count, offset, action/cancel slots, and close visibility examples. Status: completed.
- Register both anatomy cases in `ExampleCasesFor` so the Feedback category has complete per-case source expansion coverage. Status: completed.
- Add Docs registration assertions and scoped chrome tests for Empty State actions plus Sonner/Toast close-button templates. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~OverlayFeedbackComponentTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`

### Agent C102: Switch And Toggle Checked/Pressed Event Parity

Status: completed for this Web event parity and Docs interaction slice. This slice keeps the existing per-case Docs code expansion model intact while making Switch and standalone Toggle expose explicit Web-style checked/pressed change events.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSwitch.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexToggle.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SwitchInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- Web/Radix Switch exposes `checked` plus `onCheckedChange`; local `CodexSwitch` only had native `IsChecked` and content state before this slice.
- Web/Radix Toggle exposes `pressed` plus `onPressedChange`; local `CodexToggle` already mapped `IsPressed` to `IsChecked` and state classes but did not expose a dedicated pressed-change event.
- Docs already renders every `DocsExampleCase` through `BuildInlineExample`, so the correct fix is to enrich the rendered interaction cases and AXAML samples while preserving the `Show code` / `Hide code` button directly below each component case.

Implementation targets:

- Add `CodexSwitchCheckedChangedEventArgs` and `CodexSwitch.CheckedChanged` with normalized old/new boolean values. Status: completed.
- Add `CodexTogglePressedChangedEventArgs` and `CodexToggle.PressedChanged` with normalized old/new boolean values. Status: completed.
- Route programmatic, pointer, and keyboard paths through the same native checked property so events fire once per effective boolean change. Status: completed.
- Update Docs Switch/Toggle notes and event matrix to document `CheckedChanged` and `PressedChanged` as Web `onCheckedChange` / `onPressedChange` parity. Status: completed.
- Update Switch and Toggle interaction previews so live status text is driven by the new events, with the AXAML sample still available from the local inline code expansion under the rendered case. Status: completed.
- Update docs-site Forms pages in English and Chinese to call out Switch/Toggle event parity and per-case AXAML expansion. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C103: Accordion ValueChanged Source Metadata

Status: completed for this Navigation event-parity slice. This pass preserves the existing `OldValues` / `NewValues` API while adding changed item, value, index, open state, and source metadata so Accordion hosts can observe Web-style value changes without guessing which trigger or property path caused the update.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexAccordion.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/AccordionInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- Radix Accordion exposes `value` / `defaultValue` / `onValueChange`, `type=single|multiple`, `collapsible`, `orientation`, and disabled item behavior.
- Local Accordion already had single/multiple, collapsible, orientation keys, roving trigger focus, and old/new open-value arrays, but the event did not report the changed item, changed value, item index, open/close direction, or whether the path was trigger, keyboard, programmatic, or normalization.
- Docs already renders every Accordion case above its local `Show code` / `Hide code` AXAML block, so the interaction case only needed richer event text while preserving the per-case code reveal model.

Implementation targets:

- Add `CodexAccordionValueChangeSource` with `Programmatic`, `Trigger`, `Keyboard`, and `Normalization`. Status: completed.
- Extend `CodexAccordionValueChangedEventArgs` with `OldValue`, `NewValue`, `ChangedItem`, `ChangedIndex`, `ChangedValue`, `Source`, and `IsOpen` while preserving the existing constructor shape through optional parameters. Status: completed.
- Route pointer trigger activation, keyboard Enter/Space activation, direct `IsOpen` changes, and normalization through source-aware value updates without duplicate events. Status: completed.
- Update Accordion behavior tests for keyboard source metadata, programmatic source metadata, changed item/index/value, open/close direction, and multiple-mode events. Status: completed.
- Update Docs page notes, event matrix, interaction status text, local AXAML sample, and docs-site Navigation EN/ZH pages. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`

### Agent C107: Text Input Anatomy Docs And Default Style Guard

Status: completed for this Forms Docs coverage and input-style fidelity slice. This pass completes the TextBox and Textarea Docs pages with Anatomy examples and strengthens guards around native Avalonia text input chrome.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/TextBoxAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/TextareaAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- `forms.textbox` and `forms.textarea` were still below four Docs cases after the Feedback passes, and neither page had a dedicated anatomy sample for the owned input template parts.
- Both controls inherit from Avalonia `TextBox`, so Web parity depends on replacing the default presenter chrome with Codex-owned `PART_BorderElement`, `PART_ScrollViewer`, `PART_Placeholder`, `PART_TextPresenter`, selection/caret resources, and focus-visible ring behavior.
- The existing Docs inline example path already renders the case first and places the local sample code under the component via `BuildInlineExample`, so adding registered AXAML files keeps the code reveal requirement intact.

Implementation targets:

- Add TextBox anatomy Docs case covering placeholder, presenter, inline left/right slots, selection, caret, intent, and size examples. Status: completed.
- Add Textarea anatomy Docs case covering multiline wrapping, placeholder, scroll viewer, selection, caret, tall layout, and semantic intent examples. Status: completed.
- Register both cases in `ExampleCasesFor` and assert their independent sample registration in Docs tests. Status: completed.
- Add text-input template guard checks for owned presenter/placeholder/focus chrome and against default Avalonia/Fluent parts such as content host, watermark, or BasedOn defaults. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~FormComponentDetailTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`

### Agent C108: Icon And Split Button Anatomy Docs And Chrome Guard

Status: completed for this Forms Docs coverage and button chrome fidelity slice. This pass completes the Icon Button and Split Button Docs pages with Anatomy examples and locks down their Codex-owned button and popup chrome.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/IconButtonAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SplitButtonAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`

Evidence gathered:

- `forms.icon-button` and `forms.split-button` were still below four Docs cases after the text-input pass, and neither page had a dedicated anatomy sample for inherited button chrome or split popup parts.
- `CodexIconButton` intentionally derives from `CodexButton`, sets `Size=Icon`, and adds a `round` class; Web parity depends on retaining the CodexButton template instead of introducing a separate default Button template path.
- `CodexSplitButton` composes `PART_PrimaryAction` and `PART_MenuTrigger` as `CodexButton` children plus owned divider, chevron, popup surface, arrow, alignment, and open/closed motion classes.

Implementation targets:

- Add Icon Button anatomy Docs case covering icon-sized chrome, round geometry, loading slot, semantic variants, disabled state, and toolbar rhythm. Status: completed.
- Add Split Button anatomy Docs case covering primary action, menu trigger, divider, chevron, popup surface, arrow visibility, alignment, loading/disabled, and close policy. Status: completed.
- Register both cases in `ExampleCasesFor` and assert their independent sample registration in Docs tests. Status: completed.
- Add chrome guard checks proving IconButton inherits CodexButton geometry and SplitButton uses scoped CodexButton children plus tokenized popup motion rather than raw/default Button styling. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~FormComponentDetailTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`

### Agent C104: Menu ItemSelected OnSelect Metadata

Status: completed for this Navigation event-parity slice. This pass adds Web `onSelect`-style leaf-selection metadata for Menu, Context Menu, and Menubar item paths while keeping submenu triggers as disclosure controls instead of select events.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexContextMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/MenuInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/ContextMenuInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/MenubarInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MenuRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- Radix Dropdown Menu, Context Menu, and Menubar expose leaf item selection through item `onSelect` while checkbox/radio items keep checked state and submenu triggers remain disclosure controls.
- Local menu controls already owned active, disabled, loading, submenu open/close, shortcut, checkbox, radio, and close-on-select behavior, but hosts had no typed selected-item event metadata for source, checked/radio state, command parameter, or whether close-on-select actually dismissed a surface.
- Avalonia can raise click after key handling, so source tracking must survive dispatcher-posted keyboard click delivery instead of clearing at the end of `OnKeyDown`.
- Docs already renders Menu, Context Menu, and Menubar interaction cases before local AXAML source toggles, so the interaction text was enriched without changing the per-case code reveal model.

Implementation targets:

- Add `CodexMenuItemSelectSource` with `Programmatic`, `Pointer`, and `Keyboard`. Status: completed.
- Add `CodexMenuItemSelectedEventArgs` with selected item, header, command parameter, toggle type, checked state, submenu state, source, and close-on-select result. Status: completed.
- Add `ItemSelected` to `CodexMenuItem` and `CodexContextMenuItem`; Menubar leaf items inherit the same event contract from `CodexMenuItem`. Status: completed.
- Track pointer and keyboard activation source across Avalonia's deferred click delivery, and emit selection only for leaf items. Status: completed.
- Add rendered lifecycle tests proving Menu, Context Menu, and Menubar leaf selection metadata, keyboard source, checkbox state, command parameter, submenu non-selection, and close-on-select behavior. Status: completed.
- Update Docs notes, event matrix, Menu/ContextMenu/Menubar interaction previews, AXAML interaction samples, and docs-site Navigation EN/ZH pages. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~MenuRenderedLifecycleTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~MenuRenderedLifecycleTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`

Carry-forward style fidelity note:

- Future visual/style slices should explicitly check for default Avalonia/Fluent style leakage in mounted templates, not only public API parity, because default paddings, focus chrome, popup offsets, and pressed/hover surfaces can make the actual rendered effect diverge from the Web target.

### Agent C81: Slider Web Value Events And Docs Visual Debug

Status: completed for this Forms event-parity slice. This pass keeps the existing `CodexSlider` surface but adds the Web-style value lifecycle, upgrades the Docs interaction case, fixes the track rendering bug found during visual inspection, and refreshes the Docs visual fingerprints.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSlider.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Slider.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SliderInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- Web/Radix Slider distinguishes live value updates (`onValueChange`) from commit updates (`onValueCommit`), and exposes values as arrays for range/multiple-thumb parity.
- Local `CodexSlider` previously only relied on native `Value`, so Docs could not show an event contract equivalent to Web.
- Actual Docs screenshot capture of `forms.slider` with inline code expanded showed that the thumb rendered but the track line collapsed to 1px button visuals. The fix separates the visual track from the hidden Track hit-test buttons.

Implementation targets:

- Add `CodexSliderValueChangingEventArgs`, `CodexSliderValueCommittedEventArgs`, `ValueChanging`, `ValueCommitted`, and `CommitValue()` with pointer, keyboard, focus, and programmatic commit sources. Status: completed.
- Add `slider`, `has-value`, `at-min`, `at-max`, and `dragging` classes, with pointer press/release and keyboard release commit handling. Status: completed.
- Update `Slider.axaml` so the template draws full-width/full-height track background and filled track borders while keeping the `Track` control responsible for input and thumb position. Status: completed.
- Add a visible `dragging` thumb transform state and keep tokenized transitions for brush, opacity, size, and transform motion. Status: completed.
- Update Docs page notes, state matrix, event matrix, and interaction preview to show `ValueChanging` / `ValueCommitted` status plus host-triggered commit buttons. Status: completed.
- Update `Forms/SliderInteraction.axaml` so the expanded code under the rendered example mirrors the current case structure. Status: completed.
- Update docs-site Forms pages in English and Chinese to document slider value-change/value-commit parity and dragging motion. Status: completed.

Visual debug:

- Created and removed a temporary headless capture test that navigated to `forms.slider`, clicked `Set 76` and `Commit`, expanded all inline code blocks, asserted the programmatic `ValueCommitted` status, and saved `/tmp/codexswitchui-docs-slider-expanded.png`.
- Manual image inspection found and confirmed the Slider track rendering fix. The final capture shows visible filled and unfilled tracks with inline code expanded under the rendered example.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`
- Temporary screenshot capture test for `forms.slider`, removed after inspection.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsRenderedLifecycleTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C82: Tabs Value, Activation Mode, And Docs Lifecycle Parity

Status: completed for this Navigation event-parity and Docs visual-debug slice. This pass upgrades `CodexTabs` toward the Radix/shadcn contract with explicit values, value-change events, automatic/manual activation, loop boundaries, and generated-container value mirroring, then fixes a Docs template lifecycle hang found during full rendered-page verification.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexTabs.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Tabs.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/TabsInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- Radix Tabs exposes controlled `value`, `defaultValue`, `onValueChange`, `orientation`, `activationMode`, and `loop`, with disabled triggers skipped by roving navigation.
- Local `CodexTabs` already used Avalonia `TabControl` selection, but it did not surface a Web-style `SelectedValue`/`ValueChanged` event contract or distinguish automatic versus manual activation.
- Headless Docs capture of `navigation.tabs` with inline code expanded showed the interaction example and confirmed a real visual bug: horizontal tab headers were rendering vertically. The root cause was the template `ItemsPresenter` panel binding path.

Implementation targets:

- Add `CodexTabsActivationMode`, `ActivationMode`, `IsLoop`, `SelectedValue`, `ValueChanged`, and `CodexTabsValueChangedEventArgs` with old/new item, index, and value details. Status: completed.
- Add `CodexTabItem.Value` plus generated-container value mirroring so item-bound tabs and explicit `CodexTabItem` markup share the same value contract. Status: completed.
- Update keyboard handling so automatic activation selects during roving focus, manual activation moves focus without selecting, loop boundaries honor `IsLoop=false`, and disabled items remain skipped. Status: completed.
- Add root classes for `tabs`, `has-selection`, `activation-automatic`, `activation-manual`, and `loop`, plus visual treatment for manual activation state. Status: completed.
- Replace the fragile template `ItemsPresenter ItemsPanel="{TemplateBinding ItemsPanel}"` path with explicit template-local horizontal and vertical panels, fixing both horizontal header rendering and the detach-time Docs lifecycle hang. Status: completed.
- Expand the Docs Tabs interaction preview and `Navigation/TabsInteraction.axaml` with value-driven tabs, programmatic value buttons, manual activation, vertical tabs, loop-disabled behavior, and local inline code expansion. Status: completed.
- Update docs-site Navigation pages in English and Chinese to document value changes, activation modes, loop boundaries, and roving trigger behavior. Status: completed.

Visual debug:

- Created and removed a temporary headless capture test that navigated to `navigation.tabs`, clicked the `Events` value button, asserted `ValueChanged: code -> events.`, expanded inline code, and saved `/tmp/codexswitchui-docs-tabs-expanded.png`.
- Created and removed a temporary multi-case progress test to isolate a close/detach hang; the hang was resolved by removing the template-bound `ItemsPanel` path.
- Manual screenshot inspection confirmed horizontal tab headers render horizontally again and that the expanded AXAML code block sits beneath the rendered example.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~TabsRenderedLifecycleTests|FullyQualifiedName~DocsPanelLayoutTests"`: 87 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes"`: passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes"`: passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsRenderedLifecycleTests`: 6 passed.
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`: 184 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 212 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C83: Calendar DayPicker Selection And Month Event Parity

Status: completed for this Forms event-parity and Docs interaction slice. This pass tightens `CodexCalendar` around the DayPicker/shadcn Calendar contract by surfacing selected-date, range, active-date, and displayed-month changes as public events, then updates the Calendar Docs interaction case so event feedback is visible above the inline AXAML source block.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCalendar.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/CalendarInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- DayPicker selection modes expose `mode`, `selected`, `disabled`, and `onSelect`, with single and range modes mapping selection changes through callback events.
- DayPicker month navigation exposes controlled `month` and `onMonthChange`, navigation bounds, and month transition animation hooks.
- Local `CodexCalendar` already rendered single/range/outside/week-number/unavailable/active states, but it did not expose observable `onSelect`/`onMonthChange` equivalents and its computed `MonthTitle` was only a CLR getter, so template binding updates after `DisplayDate` changes were not strongly guaranteed.

Implementation targets:

- Add `CodexCalendarSelectedDateChangedEventArgs`, `CodexCalendarRangeChangedEventArgs`, `CodexCalendarDisplayDateChangedEventArgs`, and `CodexCalendarActiveDateChangedEventArgs`. Status: completed.
- Add public `SelectedDateChanged`, `RangeChanged`, `DisplayDateChanged`, and `ActiveDateChanged` events fired from property changes, pointer selection, keyboard selection, active-day movement, and month navigation paths. Status: completed.
- Convert `MonthTitle` to a direct Avalonia property backed by `MonthTitleProperty` so previous/next month navigation updates the rendered title reliably. Status: completed.
- Add `range-complete`, `has-active-date`, `can-previous`, and `can-next` classes for Web-style state inspection and Docs visual checks. Status: completed.
- Update Calendar interaction Docs preview with live event status, single-date event buttons, range event buttons, and matching `Forms/CalendarInteraction.axaml` inline source. Status: completed.
- Update Calendar state/event matrices plus docs-site Forms notes in English and Chinese for selected-date, range, active-date, and month-change parity. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`: 48 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests"`: 37 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 212 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C84: Image Icon Load Error Lifecycle Event Parity

Status: completed for this Data Display event-parity and Docs interaction slice. This pass tightens `CodexImageIcon` around the Web `img` lifecycle contract by surfacing successful loads and failed loads as public events, then updates the Image icon Docs page so the rendered interaction case shows live load/error status above its inline expandable AXAML sample.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDisplayPrimitives.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ApplicationShell.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ImageIconStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ImageIconInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- Web image elements expose `load` and `error` lifecycle events, so desktop provider-logo images need observable success/failure paths instead of silently swapping or clearing `Source`.
- Local `CodexImageIcon` already resolved avares resources and cleared `Source` for missing paths, but it did not expose load/error callbacks, diagnostic state, or source-state classes.
- Local Docs already satisfy the per-case inline-code requirement through `BuildInlineExample`, so the Image icon interaction case needed only a focused event/status refresh rather than a Docs shell rewrite.

Implementation targets:

- Add `CodexImageIconLoadedEventArgs`, `CodexImageIconLoadFailedEventArgs`, `ImageLoaded`, and `ImageLoadFailed`. Status: completed.
- Add `HasSource`, `IsMissing`, `IsEmpty`, and `LastLoadError` state plus `image-icon`, `has-source`, `missing-source`, and `empty-source` classes. Status: completed.
- Make local file loading use `File.OpenRead` plus `Bitmap(stream)` so file and avares loading share the same stream-backed path in headless and app runtimes. Status: completed.
- Add source-state style selectors with tokenized opacity transitions in `ApplicationShell.axaml`. Status: completed.
- Update the Image icon Docs interaction case with load/error status text, provider switching, missing asset failure state, size handoff, and matching compileable AXAML samples under the component. Status: completed.
- Update Data Display state/event matrices plus docs-site EN/ZH notes for Image icon lifecycle parity. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`: 87 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsImageIconPageLoadsLinkedAvaresResources"`: 4 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 213 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C85: Avatar Image Loading Status And Fallback Delay Parity

Status: completed for this Feedback event-parity and Docs interaction slice. This pass tightens `CodexAvatar` around the Radix/shadcn Avatar contract by adding image loading status, delayed fallback visibility, and observable status changes while preserving the existing manual `Source` API.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexAvatar.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Avatar.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/Avatar.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/AvatarStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/AvatarInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/feedback.mdx`
- `docs-site/content/docs/zh/ui-system/feedback.mdx`

Evidence gathered:

- Radix Avatar documents `Avatar.Image` as rendering only after it has loaded and exposes `onLoadingStatusChange` for additional control.
- Radix Avatar `Fallback` renders while the image is loading or errored, and supports `delayMs` to avoid a fast loading flash.
- shadcn Avatar composes `AvatarImage`, `AvatarFallback`, `AvatarBadge`, `AvatarGroup`, and `AvatarGroupCount`, so the Avalonia control should keep image, fallback, status badge, and group states inspectable.
- Local `CodexAvatar` already had `Source`, `Fallback`, status dot, variants, sizes, and group support, but did not own path loading, loading/error events, or delayed fallback state.

Implementation targets:

- Add `CodexAvatarLoadingStatus`, `CodexAvatarLoadingStatusChangedEventArgs`, `LoadingStatusChanged`, `ImagePath`, `LoadingStatus`, `FallbackDelay`, `IsFallbackVisible`, and `LastLoadError`. Status: completed.
- Add stream-backed avares/local file loading for `ImagePath`, while keeping manual `Source` assignment as a supported path that updates loaded/idle status. Status: completed.
- Add `avatar`, `loading`, `loaded`, `error`, `idle`, `fallback-visible`, and `fallback-delayed` classes alongside existing variant, size, fallback, image, and status classes. Status: completed.
- Update `Avatar.axaml` to bind fallback visibility to `IsFallbackVisible` and add tokenized opacity transitions for image/fallback lifecycle states. Status: completed.
- Update Avatar Docs preview, states, and interaction examples so rendered cases show loaded images, error fallback, delayed fallback, `LoadingStatusChanged`, size changes, and disabled host composition with inline expandable AXAML. Status: completed.
- Update Feedback state/event matrices and docs-site EN/ZH notes for Avatar lifecycle parity. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`: 36 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 214 passed.

### Agent C86: Tooltip OpenChange And Keyboard Dismiss Parity

Status: completed for this Overlay event-parity and Docs interaction slice. This pass tightens `CodexTooltip` against the current Radix Tooltip contract by making tooltips closed by default, exposing `OpenChanged` as the Avalonia equivalent of Web `onOpenChange`, opening immediately from keyboard focus, and closing from Escape, Enter, and Space while keeping the trigger mounted.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexTooltip.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/TooltipInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- Radix Tooltip documents Provider delay defaults, `open` / `defaultOpen` / `onOpenChange`, hover and focus opening, Escape dismissal, and trigger Enter/Space closing behavior.
- Local Tooltip already had provider, trigger, open/closed, side placement, arrow, request-open/request-close timers, and Escape close, but defaulted open and had no `OpenChanged` event.
- Local Docs already render each example first and place the per-case inline AXAML expand/copy surface directly below it, so this pass only needed the Tooltip interaction case refreshed rather than another Docs shell rewrite.

Implementation targets:

- Add `CodexTooltipOpenChangedEventArgs` and `OpenChanged`, raised whenever `IsOpen` changes through pointer, focus, keyboard, or manual APIs. Status: completed.
- Change `IsOpen` default to closed to match Web `defaultOpen=false`, while keeping visual Docs examples explicitly open where the state is intentionally shown. Status: completed.
- Add provider-aware open delay resolution for unset tooltip `OpenDelay`, plus `hoverable-disabled` state inherited from `CodexTooltipProvider` when the tooltip has not opted out. Status: completed.
- Add `RequestFocusOpen()` so keyboard focus opens immediately without the hover delay, and expand `TryHandleDismissKey` so Escape, Enter, and Space close an open tooltip according to Web keyboard interaction. Status: completed.
- Update Tooltip Docs behavior notes, state matrix, event matrix, interaction preview, and compileable AXAML sample so the rendered case shows live `OpenChanged` status and manual open/dismiss controls above its inline code block. Status: completed.
- Update docs-site Overlay notes in English and Chinese for immediate focus open, `OpenChanged`, and keyboard dismissal parity. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~MotionRenderedLifecycleTests|FullyQualifiedName~DocsPanelLayoutTests"`: 48 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 214 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C87: Popover Trigger OpenChange And Surface Motion Parity

Status: completed for this Overlay Root/Trigger/Content event-parity slice. This pass moves `CodexPopover` from a standalone panel toward the Radix/shadcn Popover contract by adding trigger composition, default-closed open state, Web-style `OpenChanged`, Enter/Space trigger toggling, optional arrow state, side/align classes, and surface-only closed motion so the trigger remains visible.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexPopover.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Popover.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/Popover.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/PopoverStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/PopoverAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/PopoverInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- Radix Popover documents Root with `open`, `defaultOpen`, `onOpenChange`, and `modal=false`; Trigger toggles open/closed with Space and Enter; Content exposes `data-state`, `data-side`, and `data-align`; optional Arrow visually connects the content to the anchor.
- Local `CodexPopover` already had header/body/action/close slots, Escape/outside dismissal, focus restore, and open/closed classes, but it defaulted open, hid the whole control when closed, and lacked trigger/open-change composition.
- Local Docs already satisfy the per-case inline AXAML requirement through `BuildInlineExample`, so this pass refreshed the Popover examples and kept the component preview rendered above its expandable code block.

Implementation targets:

- Add `Trigger`, `TriggerTemplate`, `HasTrigger`, `CodexPopoverOpenChangedEventArgs`, `OpenChanged`, `Open()`, `Toggle()`, trigger click routing, and trigger Enter/Space keyboard toggling. Status: completed.
- Change `IsOpen` default to closed, while updating visual state/anatomy samples to set `IsOpen=true` where an always-visible surface is intentional. Status: completed.
- Add `Placement`, `Align`, `IsArrowVisible`, side classes, align classes, `trigger-open`, `trigger-closed`, and `has-arrow` state. Status: completed.
- Update `Popover.axaml` with `PART_Root`, `PART_Trigger`, surface-only open/closed opacity/scale motion, side-aware closed transforms, optional arrow, and trigger-safe templates so closed content no longer hides the trigger. Status: completed.
- Update Popover Docs default, states, anatomy, and interaction examples so the rendered interaction case shows `OpenChanged` status, trigger-owned toggling, dismiss command, persistent policy, closed state, and action content above its inline code block. Status: completed.
- Update docs-site Overlay notes in English and Chinese for trigger/content/close/arrow composition, `OpenChanged`, Enter/Space toggling, side/align motion, dismissal, and focus return. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~MotionRenderedLifecycleTests|FullyQualifiedName~DocsPanelLayoutTests"`: 49 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 215 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C88: Dialog Trigger OpenChange Modal Surface Parity

Status: completed for this Overlay Root/Trigger/Overlay/Content event-parity slice. This pass moves `CodexDialog` and its derived overlay surfaces toward the Radix/shadcn Dialog contract by adding trigger composition, default-closed open state, Web-style `OpenChanged`, Enter/Space trigger toggling, modal/non-modal state, and surface-only exit motion so the trigger remains visible.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Dialog.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/Dialog.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DialogStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DialogAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DialogInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- Radix Dialog documents Root with `open`, `defaultOpen`, `onOpenChange`, and `modal=true`; Trigger opens/closes with Space and Enter; Portal hosts Overlay and Content; Close, Escape, and outside interaction close and restore focus to the trigger.
- Local `CodexDialog` already had header/body/action/close slots, Escape/outside dismissal, focus restore, and open/closed classes, but it defaulted open, hid the whole control when closed, and lacked trigger/open-change/modal composition.
- Local Docs already render each example before its inline AXAML source through `BuildInlineExample`, so this pass refreshed the Dialog examples while preserving the per-case code reveal flow.

Implementation targets:

- Add `Trigger`, `TriggerTemplate`, `HasTrigger`, `CodexDialogOpenChangedEventArgs`, `OpenChanged`, `Open()`, `Toggle()`, trigger click routing, and trigger Enter/Space keyboard toggling. Status: completed.
- Change `IsOpen` default to closed on the Dialog base contract, then update Dialog, Sheet, Drawer, AlertDialog, and CommandDialog tests/examples to set `IsOpen=true` where an open visual state is intentional. Status: completed.
- Add `IsModal` with `modal` and `non-modal` classes, plus `trigger-open` and `trigger-closed` state. Status: completed.
- Update `Dialog.axaml` with `PART_Root`, `PART_Trigger`, `PART_Overlay`, surface-only open/closed opacity/scale motion, overlay fade, and non-modal scrim suppression so closed content no longer hides the trigger. Status: completed.
- Update Dialog Docs default, anatomy, and interaction examples so the rendered interaction case shows `OpenChanged` status, trigger-owned toggling, dismiss command, manual policy, closed state, and action content above its inline code block. Status: completed.
- Update docs-site Overlay notes in English and Chinese for Dialog composition, `OpenChanged`, modal scrim, trigger keyboard toggling, dismissal, and focus return. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~OverlayRenderedLifecycleTests|FullyQualifiedName~MotionRenderedLifecycleTests|FullyQualifiedName~DocsPanelLayoutTests"`: 55 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 216 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C89: AlertDialog Trigger Response Surface Parity

Status: completed for this Overlay AlertDialog-specific response and Docs slice. This pass builds on the Dialog root work by giving `CodexAlertDialog` its own trigger/overlay/surface template, keeping the trigger visible while the response surface opens or exits, and making the Docs examples explicitly render open visual states or closed trigger states above their per-case AXAML reveal button.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexAlertDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/AlertDialog.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/AlertDialog.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/AlertDialogStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/AlertDialogAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/AlertDialogInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- Radix Alert Dialog documents `Root` with `open`, `defaultOpen`, and `onOpenChange`; `Trigger` opens the modal; `Overlay` and `Content` compose the layer; `Cancel` and `Action` are visually distinct response paths.
- Radix also documents automatic focus trapping, Escape closing with focus returning to `AlertDialog.Trigger`, and an alert dialog as a modal surface that interrupts the user and expects a response.
- Local `CodexAlertDialog` already had cancel/action commands, least-destructive cancel focus, loading suppression, destructive action styling, Escape dismissal, focus restoration, and default outside-pointer suppression, but its template did not expose `PART_Trigger`/`PART_Overlay` and its closed motion still lived on the control root.
- Local Docs already satisfy the requested example/code flow through `BuildInlineExample`: rendered component first, then the `Show code` button, then the selectable/copyable `DocsCodeBlock` for that exact AXAML sample.

Implementation targets:

- Add actual overlay pointer routing to `CodexDialog` by wiring `PART_Overlay` to `TryDismissFromOutsidePointer()`, so Dialog-derived templates can provide real outside-pointer behavior when they expose an overlay. Status: completed.
- Keep `CodexAlertDialog` modal, response-required by default, and update its `response-required` / `outside-dismissable` classes when `DismissOnOutsidePointer` changes. Status: completed.
- Update `AlertDialog.axaml` with `PART_Root`, `PART_Trigger`, `PART_Overlay`, and `PART_Surface`; move open/closed opacity and scale transitions to the surface and overlay so closing no longer hides the trigger or animates the detached control root. Status: completed.
- Refresh AlertDialog Docs state, anatomy, and interaction previews so open visual examples set `IsOpen=true`, closed examples keep a visible trigger, and interaction examples use `Trigger`, inherited `OpenChanged`, cancel/action command paths, async loading, and manual outside-policy samples. Status: completed.
- Update independent AlertDialog AXAML samples so each rendered case has matching local source under its own `Show code` toggle. Status: completed.
- Update docs-site Overlay pages in English and Chinese to call out AlertDialog trigger/content composition, `OpenChanged`, modal scrim, cancel/action paths, least-destructive focus, and required-response outside policy. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 56 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayRenderedLifecycleTests|FullyQualifiedName~MotionRenderedLifecycleTests|FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 20 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 216 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C90: Sheet Trigger Edge Surface Parity

Status: completed for this Overlay Sheet Root/Trigger/Content slice. This pass moves `CodexSheet` from a standalone edge panel toward the shadcn Sheet contract by composing a visible trigger, modal overlay, and edge-mounted content surface; closed state now animates only the surface and overlay so the trigger remains visible and Docs examples keep their rendered component above the per-case AXAML reveal button.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Sheet.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/Sheet.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/SheetStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/SheetAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/SheetInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- shadcn Sheet documents Sheet as extending Dialog, with `SheetTrigger` plus `SheetContent`, side placement on the content, optional close button, and top/right/bottom/left sides.
- Radix Dialog evidence still applies because shadcn Sheet is Dialog-based: controlled `open` / `onOpenChange`, overlay/content composition, Escape and outside dismissal, and focus return to the trigger.
- Local `CodexSheet` already inherited Dialog's default-closed, trigger, `OpenChanged`, dismiss, Escape/outside, and focus-restore API, but its template had only `PART_Surface`; root-level closed opacity/transform could hide or move any trigger added by hosts.
- Local Docs already satisfy the required example/code flow through `BuildInlineExample`, so the Sheet work needed refreshed independent AXAML samples rather than another Docs shell rewrite.

Implementation targets:

- Update `Sheet.axaml` with `PART_Root`, `PART_Trigger`, `PART_Layer`, `PART_Overlay`, and `PART_Surface`, keeping trigger composition visible while open/closed state affects only overlay and content. Status: completed.
- Move side-specific open/closed slide motion to `PART_Surface`, keep overlay fade on `PART_Overlay`, and preserve top/right/bottom/left side classes and close button styling. Status: completed.
- Refresh Sheet Docs default, states, anatomy, and interaction previews so open visual examples set `IsOpen=true`, closed examples keep a visible trigger, and interaction examples use inherited `OpenChanged`, trigger toggling, side cycling, dismiss command, manual policy, and focus return. Status: completed.
- Update independent Sheet AXAML samples so each rendered case has matching local source under its own `Show code` toggle. Status: completed.
- Update docs-site Overlay pages in English and Chinese to call out Sheet trigger/content composition, `OpenChanged`, modal scrim, side placement, edge motion, close control, and focus return. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~OverlayRenderedLifecycleTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 61 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~MotionRenderedLifecycleTests"`: 15 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 216 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C91: Drawer Trigger Vaul Surface Parity

Status: completed for this Overlay Drawer Root/Trigger/Content slice. This pass moves `CodexDrawer` closer to the current shadcn/Vaul drawer composition by keeping the trigger mounted outside the animated content surface, routing open changes through the inherited dialog contract, and refreshing Docs so every Drawer case renders the component first and exposes the matching AXAML under that exact case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Drawer.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/Drawer.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DrawerStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DrawerAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DrawerInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- shadcn Drawer still documents a Drawer root with `DrawerTrigger` and `DrawerContent`, backed by Vaul-style behavior for open state, directions, handles, scrollable bodies, and footer actions.
- Local `CodexDrawer` already inherited Dialog's default-closed state, `Trigger`, `OpenChanged`, Escape/outside dismissal, focus restoration, and drag-dismiss contract, but its template animated only a root-level surface and did not expose the trigger/overlay/content composition in the same way Sheet and Dialog now do.
- Local Docs already satisfy the requested per-case code reveal flow through `BuildInlineExample`, so this pass refreshed the Drawer previews and AXAML samples instead of rewriting the Docs shell.

Implementation targets:

- Update `Drawer.axaml` with `PART_Root`, `PART_Trigger`, `PART_Layer`, `PART_Overlay`, and `PART_Surface`, keeping trigger composition visible while open/closed state affects only overlay and content. Status: completed.
- Move direction-specific open/closed motion to `PART_Surface`, keep overlay fade on `PART_Overlay`, preserve direction, handle, drag-ready, scroll body, footer, close, and non-modal classes. Status: completed.
- Refresh Drawer Docs default, states, anatomy, and interaction previews so open visual examples set `IsOpen=true`, closed examples keep a visible trigger, and interaction examples use inherited `OpenChanged`, trigger toggling, drag threshold/release, direction cycling, manual policy, and focus return. Status: completed.
- Update independent Drawer AXAML samples so each rendered case has matching local source under its own `Show code` toggle. Status: completed.
- Update docs-site Overlay pages in English and Chinese for Drawer trigger/content composition, `OpenChanged`, modal scrim, directions, handle drag threshold/release, sticky footer actions, close controls, and focus return. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~OverlayRenderedLifecycleTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 61 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~MotionRenderedLifecycleTests"`: 15 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 216 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C92: CommandDialog Trigger Command Surface Parity

Status: completed for this Overlay CommandDialog Root/Trigger/Content slice. This pass moves `CodexCommandDialog` from an older single-surface palette toward the shadcn CommandDialog example shape: an Open Menu trigger controls a mounted dialog layer, while CommandInput, CommandList, Empty, Group, Separator, Item, and Shortcut remain inside the animated command surface.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/CommandDialog.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/CommandDialog.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/CommandDialogStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/CommandDialogAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/CommandDialogInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- shadcn Command still documents `CommandDialog` with an explicit trigger button that opens the palette, then renders input, list, empty, group, separator, item, and shortcut composition inside the dialog.
- Local `CodexCommandDialog` already inherited Dialog's default-closed state, `Trigger`, `OpenChanged`, Escape/outside dismissal, focus restoration, and close-on-select suppression, but its template exposed only `PART_Surface` and put closed opacity/transform on the control root.
- Local Docs already satisfy the per-case code reveal requirement through `BuildInlineExample`, so this pass refreshed the CommandDialog examples and AXAML samples while preserving the render-first then `Show code` flow.

Implementation targets:

- Update `CommandDialog.axaml` with `PART_Root`, `PART_Trigger`, `PART_Layer`, `PART_Overlay`, and `PART_Surface`, keeping the trigger mounted while open/closed state affects only the overlay and command surface. Status: completed.
- Move closed/open opacity and scale motion to `PART_Surface`, keep scrim fade on `PART_Overlay`, preserve loading, close-on-select, loop, close button, and non-modal classes. Status: completed.
- Refresh CommandDialog Docs default, states, anatomy, and interaction previews so visual examples set `IsOpen=true`, closed examples keep a visible trigger, and interaction examples surface `OpenChanged`, item selection, loading suppression, manual close, and focus return. Status: completed.
- Update independent CommandDialog AXAML samples so each rendered case has matching local source under its own `Show code` toggle. Status: completed.
- Update docs-site Overlay pages in English and Chinese for CommandDialog trigger/content composition, `OpenChanged`, input forwarding, close-on-select, loading suppression, item-selected events, and focus return. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~MotionRenderedLifecycleTests|FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~DocsPanelLayoutTests"`: 68 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 216 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C93: Dropdown And Split Button Web Event Parity

Status: completed for this Menu/Dropdown/SplitButton event-parity slice. This pass closes the gap where Docs described Web-style open behavior before `CodexDropdownButton` and `CodexSplitButton` exposed an observable `OpenChanged` contract, and it extends close-on-select so menu leaf items inside dropdown surfaces dismiss like Web dropdown menu selections while submenu triggers remain open.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDropdownButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSplitButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/DropdownButtonInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SplitButtonInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- Current shadcn Dropdown Menu documents a trigger/content composition with grouped items, check/radio items, destructive items, shortcuts, and submenus.
- Current Radix Dropdown Menu root exposes `open`/`onOpenChange`; content exposes open/closed state, side, and align metadata; item and checkbox/radio item selection paths expose `onSelect`; submenus expose their own open state and should not be treated as leaf selection.
- Local `CodexMenuActivation` already owned submenu pointer delays, keyboard submenu open/close, and leaf `ShouldCloseOnSelect` semantics, so Dropdown/SplitButton could reuse that activation policy instead of inventing a second menu-selection rule.
- Local Docs already satisfy the per-case code reveal requirement through `BuildInlineExample`; this slice updated the rendered interaction cases and matching AXAML samples while preserving the inline `Show code` button under each case.

Implementation targets:

- Add `CodexDropdownButtonOpenChangedEventArgs`, `CodexSplitButtonOpenChangedEventArgs`, and public `OpenChanged` events raised whenever `IsOpen` changes through Open, Dismiss, Escape, trigger toggling, popup light dismiss binding, or close-on-select paths. Status: completed.
- Keep focus restoration after close while emitting the Web-style open boolean exactly once per state transition. Status: completed.
- Add `MenuItem.ClickEvent` handling on dropdown surfaces and expose internal `TryCloseFromDropDownMenuItem` helpers that reuse `CodexMenuActivation.ShouldCloseOnSelect`, close leaf menu items, suppress disabled/loading/command-blocked items, and keep submenu triggers open. Status: completed.
- Refresh Dropdown and SplitButton interaction Docs with live `OpenChanged` status text, updated event matrices, and independent AXAML samples under the current case's code reveal. Status: completed.
- Update docs-site Navigation and Forms pages in English and Chinese to call out `OpenChanged`, close-on-select, leaf menu item dismissal, loading suppression, and focus return. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`: 0 warnings, 0 errors.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~MotionRenderedLifecycleTests|FullyQualifiedName~MenuRenderedLifecycleTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~OverlayFeedbackComponentTests"`: 113 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsPanelLayoutTests"`: 18 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 217 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C94: Collapsible OpenChanged Web Disclosure Parity

Status: completed for this Navigation Collapsible event-parity slice. This pass brings `CodexCollapsible` closer to the current Radix/shadcn Collapsible contract by exposing an `OpenChanged` event equivalent to Web `onOpenChange`, while preserving measured height animation, deferred close unmounting, Enter/Space trigger activation, disabled suppression, and reduced-motion final states.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCollapsible.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/CollapsibleInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- Current shadcn Collapsible documents a root with `open` and `onOpenChange`, plus `CollapsibleTrigger` and `CollapsibleContent` composition.
- Current Radix Collapsible documents root `open`/`onOpenChange`, `disabled`, trigger/content open and closed data-state, and content width/height variables for open/close animation.
- Local `CodexCollapsible` already had trigger click, Enter/Space, measured height animation, deferred close visibility, open/closed classes, and reduced-motion resolution, but no public open-change event for controlled hosts or Docs interaction status.
- Local Docs already render each example before the inline `Show code` / `Hide code` AXAML block, so this slice only refreshed the interaction case and matching local AXAML sample.

Implementation targets:

- Add `CodexCollapsibleOpenChangedEventArgs` and public `OpenChanged`, emitted once whenever `IsOpen` changes through pointer, keyboard, or programmatic state paths. Status: completed.
- Route `IsOpenProperty` changes through an open-changed handler that keeps height animation and class synchronization intact before publishing the Web-style boolean. Status: completed.
- Update Collapsible state and event matrices so open/closed trigger behavior explicitly calls out `OpenChanged`. Status: completed.
- Refresh the Collapsible interaction preview and AXAML sample with live `OpenChanged` status above the rendered disclosure grid. Status: completed.
- Update docs-site Navigation pages in English and Chinese for measured content height animation, trigger click/Enter/Space toggling, `OpenChanged`, and reduced-motion final states. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`: 0 warnings, 0 errors.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~MotionRenderedLifecycleTests|FullyQualifiedName~DocsPanelLayoutTests"`: 99 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 217 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C95: SegmentedControl ValueChanged Web Selection Parity

Status: completed for this Navigation SegmentedControl value-event slice. This pass moves `CodexSegmentedControl` from local sibling selection plus a moving indicator toward the Web segmented/toggle-group contract by adding a root-owned `SelectedValue`, button `Value`, and `ValueChanged` event with old/new item, index, and value metadata.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNavigationPrimitives.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SegmentedControlInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- Web segmented controls are commonly modeled as a single-value toggle group; Radix Toggle Group exposes `value`, `onValueChange`, item `value`, disabled items, and roving/loop behavior.
- Local `CodexSegmentedControl` already measured the selected segment and animated `PART_Indicator`, but selected state lived only on sibling buttons and had no root-level value event for controlled hosts.
- Local command-backed segmented buttons intentionally skip local sibling selection so app/view-model-controlled selection can stay external; this behavior needed to remain intact.
- Local Docs already satisfy the per-case code reveal flow, so this slice updated only the interaction preview and its matching local AXAML source.

Implementation targets:

- Add `CodexSegmentedControlValueChangedEventArgs` with old/new segmented button, old/new index, and old/new string value metadata. Status: completed.
- Add `SelectedValue` and `ValueChanged` to `CodexSegmentedControl`, plus `Value` to `CodexSegmentedButton`. Status: completed.
- Route normal segment clicks through the owning `CodexSegmentedControl`, syncing sibling selected states, updating `SelectedValue`, raising `ValueChanged`, and queuing the indicator re-measure. Status: completed.
- Preserve command-backed segmented button behavior so command segments can keep selection controlled externally. Status: completed.
- Refresh SegmentedControl Docs interaction examples and AXAML samples with explicit values, live `ValueChanged` status, disabled item behavior, and indicator remeasure notes. Status: completed.
- Update docs-site Navigation pages in English and Chinese for `SelectedValue`, `ValueChanged`, and measured moving indicator parity. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`: 0 warnings, 0 errors.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~NavigationDataComponentTests"`: 107 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 218 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C96: SideNav Root ValueChanged Web Selection Parity

Status: completed for this Navigation SideNav value-event slice. This pass upgrades side navigation from item-local sibling selection to a Web-style root-owned value contract while keeping the legacy standalone `CodexSideNavItem` fallback intact for existing usages.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNavigationPrimitives.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ApplicationShell.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SideNav.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SideNavStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SideNavInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- Local `CodexSideNavItem` previously owned only `IsSelected` and selected siblings from its parent, so hosts had no root `SelectedValue` or event equivalent to Web navigation state.
- Local `CodexSegmentedControl` had just established the same root-owned `SelectedValue`, item `Value`, and `ValueChanged` pattern, so SideNav can share that API shape for consistency.
- shadcn-style sidebar/navigation buttons expose active state from the surrounding navigation model; the Avalonia equivalent is a `CodexSideNav` root that owns value state and syncs active rows.
- Local Docs already place each rendered example before the inline `Show code` / `Hide code` AXAML block through `BuildInlineExample`, so this slice refreshed the SideNav examples without changing the code-reveal mechanism.

Implementation targets:

- Add `CodexSideNavValueChangedEventArgs` with old/new side-nav item, old/new index, and old/new string value metadata. Status: completed.
- Add `CodexSideNav` root with `SelectedValue` and `ValueChanged`, including logical/visual descendant item discovery and attach/content synchronization. Status: completed.
- Add `Value` to `CodexSideNavItem`; clicks now route through the owning `CodexSideNav` when present and preserve old sibling selection when no root exists. Status: completed.
- Add a lightweight `CodexSideNav` theme template so the root participates in tokenized background/border transitions while the rows keep their existing hover, selected, pressed, and disabled motion. Status: completed.
- Refresh SideNav Docs default, states, and interaction previews plus AXAML samples with root-owned values and live `ValueChanged` status. Status: completed.
- Update docs-site Navigation pages in English and Chinese for root `SelectedValue`, item `Value`, `ValueChanged`, and legacy fallback behavior. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`: 0 warnings, 0 errors.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~NavigationDataComponentTests"`: 108 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 219 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C97: Breadcrumb LinkActivated Web Routing Parity

Status: completed for this Navigation Breadcrumb routing-event slice. This pass closes the gap where Breadcrumb had shadcn-style Root/List/Item/Link/Page/Separator/Ellipsis anatomy and current-page suppression, but hosts still needed to attach per-link `Click` handlers instead of listening to a root-owned activation event.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexBreadcrumb.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/BreadcrumbInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- Current shadcn Breadcrumb documents explicit `Breadcrumb`, `BreadcrumbList`, `BreadcrumbItem`, `BreadcrumbLink`, `BreadcrumbPage`, `BreadcrumbSeparator`, and `BreadcrumbEllipsis` composition, plus examples for collapsed/dropdown and current page behavior.
- Local `CodexBreadcrumbLink` already had `Href`, `TryActivate`, focus-visible state, and current-page suppression, but the root had no event equivalent for app-level route handling.
- Local Docs already describe ancestor link activation and current-page suppression, so the missing behavior was the root event contract and interaction status rather than another static layout example.
- Local Docs already satisfy the per-case code reveal requirement through `BuildInlineExample`, so this slice refreshed the rendered Breadcrumb interaction case and matching AXAML sample while preserving the inline `Show code` button below the case.

Implementation targets:

- Add `CodexBreadcrumbLinkActivatedEventArgs` with link, item, index, href, and content metadata. Status: completed.
- Add root `CodexBreadcrumb.LinkActivated`, raised from `CodexBreadcrumbLink.OnClick` and therefore from `TryActivate`. Status: completed.
- Suppress root activation for current links and disabled links while preserving existing command execution for active links. Status: completed.
- Refresh Breadcrumb state/event matrices plus the interaction preview with live `LinkActivated` status showing index and href. Status: completed.
- Update `BreadcrumbInteraction.axaml` to document the root event metadata alongside current-page and disabled-route examples. Status: completed.
- Update docs-site Navigation pages in English and Chinese for `LinkActivated`, href metadata, current-page suppression, and dropdown composition. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`: 0 warnings, 0 errors.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`: 87 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 219 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C98: Carousel SelectionChanged Metadata Web API Parity

Status: completed for this Data Display Carousel event-parity slice. This pass keeps the existing `SelectionChanged` event name while making its payload more like shadcn/Embla carousel select handlers: hosts can still read old/new selected indexes, and can now also inspect old/new items plus the selection source that moved the snap.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCarousel.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/CarouselInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- Current shadcn Carousel wraps Embla and exposes root/content/item/previous/next composition, API access, previous/next methods, selected snap state, and `select` event handlers for external state.
- Local `CodexCarousel` already had selected-index state, loop, orientation, commands, keyboard movement, and `SelectionChanged`, but the event args only exposed old/new index.
- Local Docs already had Carousel default, states, composition, and interaction examples with per-case code reveal, so this slice refreshed the interaction event contract rather than adding another static example.
- Boundary behavior was already guarded by `CanGoPrevious`/`CanGoNext`; source metadata needed to preserve those guards while distinguishing command, keyboard, and programmatic selection paths.

Implementation targets:

- Add `CodexCarouselSelectionChangeSource` with Programmatic, Previous, Next, First, Last, and Keyboard sources. Status: completed.
- Extend `CodexCarouselSelectionChangedEventArgs` with `OldItem`, `NewItem`, and `Source` while preserving old/new index properties. Status: completed.
- Route `SelectIndex`, `GoPrevious`, `GoNext`, `GoFirst`, `GoLast`, and keyboard navigation through source-aware selection paths. Status: completed.
- Refresh Carousel Docs page copy, state/event matrices, live interaction status, and AXAML sample copy to expose source and item metadata. Status: completed.
- Update docs-site Data Display pages in English and Chinese to document `SelectionChanged` source metadata and old/new item metadata. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`: 0 warnings, 0 errors.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`: 87 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 219 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C99: Pagination PageChanged Source Metadata Web Parity

Status: completed for this Data Display Pagination event-parity slice. This pass preserves the existing `PageChanged` event and `SelectPage(int)` host API while adding source metadata so hosts can distinguish numbered page clicks, action buttons, keyboard navigation, and programmatic changes.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexPagination.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/PaginationInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- Local `CodexPagination` already matched Web pagination structure with 1-based pages, boundary/sibling ellipses, first/previous/next/last actions, loading suppression, compact mode, and keyboard navigation.
- The event payload only exposed old/new page numbers, which made Web-style page change handlers unable to tell whether the change came from a page item, action, keyboard event, or host call.
- Docs already place each rendered example before its local `Show code` / `Hide code` AXAML block, so this slice refreshed the interaction case rather than changing the code reveal model.

Implementation targets:

- Add `CodexPaginationPageChangeSource` with Programmatic, PageItem, Previous, Next, First, Last, and Keyboard values. Status: completed.
- Extend `CodexPaginationPageChangedEventArgs` with `Source` while preserving `OldPage`, `NewPage`, and the existing constructor default behavior. Status: completed.
- Route `SelectPage`, numbered page buttons, first/previous/next/last actions, and Home/End/Left/Right/PageUp/PageDown keyboard paths through source-aware page selection. Status: completed.
- Refresh the Docs Pagination state/event matrices plus the interaction preview with live `PageChanged` source status and a host-owned jump button. Status: completed.
- Update the Pagination interaction AXAML sample and docs-site Data Display pages in English and Chinese to document source-aware page changes. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`: 0 warnings, 0 errors.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`: 87 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 219 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C100: RadioGroup ValueChanged Metadata Web API Parity

Status: completed for this Forms RadioGroup event-parity slice. This pass preserves the existing root `SelectedValue` and `ValueChanged` API while extending the event payload so hosts can inspect old/new items and indexes in addition to old/new string values.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexRadioGroup.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/RadioGroupInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- Current Radix Radio Group documents a root `value`, `onValueChange`, item `value`, item disabled state, orientation, loop, and keyboard navigation contract.
- Local `CodexRadioGroup` already owned `SelectedValue`, item values, orientation, loop, required/loading state, disabled item skipping, and roving keyboard selection.
- The gap was in `CodexRadioGroupValueChangedEventArgs`: it only exposed old/new values, while nearby Codex selection controls such as Tabs, SegmentedControl, and SideNav already expose old/new selected items and indexes for host routing.
- Local Docs already render each RadioGroup case above its own inline AXAML code toggle, so this slice refreshed the interaction case and event documentation without changing the code reveal workflow.

Implementation targets:

- Extend `CodexRadioGroupValueChangedEventArgs` with `OldItem`, `NewItem`, `OldIndex`, and `NewIndex`, while keeping the existing old/new value constructor parameters compatible. Status: completed.
- Resolve old/new item and index metadata from the owning group at the same point `SelectedValue` is updated. Status: completed.
- Refresh RadioGroup Docs page notes, state/event matrices, and interaction status text to show value metadata parity. Status: completed.
- Update the RadioGroup interaction AXAML sample and docs-site Forms pages in English and Chinese to mention `ValueChanged` old/new item/index/value metadata. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`: 0 warnings, 0 errors.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`: 48 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 219 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C101: Combobox SelectionChanged Source Metadata Web API Parity

Status: completed for this Forms Combobox event-parity slice. This pass keeps the existing `SelectionChanged`, `InputValueChanged`, and `OpenChanged` shape while making selection events carry the metadata hosts need for shadcn-style Combobox compositions: old/new items, source indexes, display values, and the path that committed or cleared selection.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCombobox.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ComboboxInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- Current shadcn Combobox is a composition of Popover and Command where input text filters command items, `CommandItem onSelect` commits a value, the popup open state is host-visible, and clear/keyboard/item paths are separate user interactions.
- Local `CodexCombobox` already had filtering, open/close state, highlighted item movement, clear action, loading/empty states, `SelectionChanged`, `InputValueChanged`, and `OpenChanged`.
- The gap was that `SelectionChanged` only exposed old/new item objects, so Docs and host code could not distinguish keyboard Enter, item click, clear action, or host-driven selection, nor could they read old/new source indexes and display values.
- Local Docs already render each Combobox case above its own inline AXAML code toggle, so this slice refreshed the interaction copy/status rather than changing the code reveal workflow.

Implementation targets:

- Add `CodexComboboxSelectionChangeSource` with Programmatic, Item, Keyboard, and Clear sources. Status: completed.
- Extend `CodexComboboxSelectionChangedEventArgs` with old/new source indexes, old/new display values, and source metadata while preserving old/new item constructor compatibility. Status: completed.
- Route public `SelectItem`, keyboard Enter, `CodexComboboxItem` clicks, and `ClearSelection` through source-aware selection paths. Status: completed.
- Refresh Combobox Docs page notes, state/event matrices, and interaction status text to show source-aware selection metadata. Status: completed.
- Update the Combobox interaction AXAML sample and docs-site Forms pages in English and Chinese to document selection metadata. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`: 0 warnings, 0 errors.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`: 48 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 219 passed.
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C79: Select Web Event Contract And Docs Anatomy Cases

Status: completed for this Forms parity slice. This slice makes `CodexSelect` expose explicit Web-style value/open events, then expands the Docs Select page so the rendered component has default, state, anatomy, and interaction examples, each with its own `Show code` / `Hide code` AXAML block directly below the case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSelect.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SelectAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SelectInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn/Radix Select exposes value and open change callbacks, so Avalonia `CodexSelect` should not only rely on inherited `ComboBox.SelectionChanged`.
- Local `BuildInlineExample` already satisfies the required Docs code reveal pattern by rendering the component first, then placing a button-controlled `DocsCodeBlock` underneath the current example.
- The previous Docs navigation transition crash is guarded by cached pages and rendered lifecycle tests; the new Select examples were verified through the full Docs lifecycle and visual fingerprint flow.

Implementation targets:

- Add `CodexSelectValueChangedEventArgs` with old/new item, old/new index, and old/new string value metadata. Status: completed.
- Add `CodexSelectOpenChangedEventArgs` and `OpenChanged`, mapped to `IsDropDownOpen` changes. Status: completed.
- Add `ValueChanged`, `select`, `has-selection`, and `placeholder-visible` state classes while preserving delayed `popup-open` motion classes. Status: completed.
- Add Select anatomy Docs preview and `Forms/SelectAnatomy.axaml` covering trigger, selected content, chevron, popup surface, disabled item, placeholder field, and field composition. Status: completed.
- Update Select interaction Docs to demonstrate `OpenChanged`/`ValueChanged` status updates, and update EN/ZH docs-site Forms pages. Status: completed.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsRenderedLifecycleTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`

### Agent C80: Native Select Web Event Contract And Debug Visual Inspection

Status: completed for this Forms parity slice. This slice gives `CodexNativeSelect` the same explicit value/open event path as `CodexSelect`, keeps option value and optgroup semantics intact, and follows the updated goal requirement by opening the Docs debug app plus capturing the Native Select Docs page for visual inspection.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNativeSelect.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/NativeSelectInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- Native Select already owned option, optgroup, invalid, placeholder, and popup motion classes, but still relied on inherited `ComboBox.SelectionChanged` rather than a component-owned Web-style event contract.
- `CodexNativeSelectOption.Value` is the browser-style option value, so event metadata needs to report old/new option values rather than only item display content.
- Opening the Docs debug app and capturing the expanded Native Select page revealed an offscreen popup leak: the interaction example's initial `IsDropDownOpen=True` popup floated over the top default example and code block when all inline code examples were expanded.

Implementation targets:

- Add `CodexNativeSelectValueChangedEventArgs` with old/new item, old/new index, and old/new option value metadata. Status: completed.
- Add `CodexNativeSelectOpenChangedEventArgs` and `OpenChanged`, mapped to `IsDropDownOpen` changes while preserving delayed `popup-open` motion classes. Status: completed.
- Keep `has-selection` and `placeholder-visible` driven by option value semantics, so an empty option remains a placeholder. Status: completed.
- Update Native Select Docs interaction preview to subscribe to `OpenChanged` and `ValueChanged`, provide Open/Claude/Close controls, and avoid mounting an initially open offscreen popup. Status: completed.
- Update EN/ZH docs-site Forms pages and Docs event matrix to call out Native Select value/open parity. Status: completed.

Manual debug inspection:

- Started `CodexSwitchUI.Docs` in debug mode with `dotnet run --project src/CodexSwitchUI.Docs/CodexSwitchUI.Docs.csproj -f net10.0 --no-restore`.
- Captured the expanded `forms.native-select` page to `/tmp/codexswitchui-docs-native-select-expanded.png` through a temporary headless debug capture test because macOS screen capture permission blocked direct window screenshots in this environment.
- Inspected the rendered PNG and fixed the visible popup overlay leak over the default example/code block; recaptured and verified the top Native Select page rendered cleanly with inline code expanded.
- Deleted the temporary debug capture test after inspection so it does not remain in the repo.

Verification:

- `dotnet build src/CodexSwitchUI/CodexSwitchUI.csproj -f net10.0 --no-restore`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsRenderedLifecycleTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`

### Agent C65: Sidebar Provider Contract And Docs Cases

Status: completed for this layout Sidebar pass. This slice adds the shadcn-style provider/root behavior missing from the existing sidebar primitives, keeps the old primitive page intact, and registers a dedicated Docs page where every rendered case exposes its own inline AXAML source toggle through `BuildInlineExample`.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexApplicationShell.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ApplicationShell.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/Sidebar.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/SidebarStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/SidebarAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/SidebarInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `docs-site/content/docs/en/ui-system/index.mdx`
- `docs-site/content/docs/zh/ui-system/index.mdx`

Evidence gathered:

- shadcn Sidebar currently documents `SidebarProvider`, `Sidebar`, `SidebarTrigger`, `SidebarRail`, and `SidebarInset`, with `side`, `variant`, `collapsible`, controlled open state, and keyboard shortcut behavior.
- Local `CodexSidebar` primitives already cover header/content/footer/group/menu/button/action/badge/submenu composition, but did not own provider state, trigger/rail toggle paths, inset state propagation, or collapsed/offcanvas/icon classes.
- Docs already satisfy the per-example inline source contract through `BuildInlineExample`, so the missing work was a dedicated page plus standalone AXAML cases.

Implementation targets:

- Add `CodexSidebarProvider` with `IsOpen`, `IsMobileOpen`, `KeyboardShortcut`, `ShortcutModifiers`, Ctrl/Meta shortcut matching, `OpenChanged`, `Open`, `Close`, `ToggleOpen`, and `TryHandleShortcut`. Status: completed.
- Extend `CodexSidebar` with `Side`, `Variant`, `Collapsible`, `IsOpen`, expanded/collapsed/offcanvas/icon/side/variant classes, and provider state synchronization. Status: completed.
- Add `CodexSidebarTrigger`, `CodexSidebarRail`, and `CodexSidebarInset` with nearest-provider or direct-sidebar resolution, shared state classes, click toggle paths, and tokenized styles. Status: completed.
- Register a new `layout.sidebar` Docs page with default, states, anatomy, and interaction examples, all backed by independent AXAML files and inline code expansion. Status: completed.
- Add state/event matrix entries, rendered lifecycle coverage, visual fingerprints, and docs-site overview notes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C66: Toggle Group Standalone Docs Page And Spacing Contract

Status: completed for this Forms docs split. This slice separates Toggle Group from the standalone Toggle page so it matches the Web component taxonomy and so every rendered group case exposes its own inline AXAML source toggle through `BuildInlineExample`.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexToggle.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Toggle.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/Toggle.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ToggleInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ToggleGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ToggleGroupStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ToggleGroupAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ToggleGroupInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn treats Toggle and Toggle Group as separate component pages; the group owns single/multiple value, item value, spacing, vertical orientation, disabled items, and roving keyboard focus.
- Toggle Group default spacing should map to the Web `spacing=2` contract while `spacing=0` connects adjacent items and changes first/middle/last item corners.
- Local Docs already satisfy the per-example inline source contract through `BuildInlineExample`, so the missing work was a standalone page plus independent AXAML cases.

Implementation targets:

- Add `CodexToggleGroup.Spacing` with `spacing-0` through `spacing-4`, `spaced`, `connected`, and connected item position classes. Status: completed.
- Split Docs registration into `forms.toggle` and `forms.toggle-group`; Toggle now owns only standalone toggle examples, while Toggle Group owns default, states, anatomy, and interaction examples. Status: completed.
- Add standalone Toggle Group AXAML cases for single/multiple values, connected spacing, vertical layout, disabled items, roving focus, and value-change examples, each rendered before its local code toggle. Status: completed.
- Extend static Docs assertions, rendered lifecycle coverage, Form detail state tests, style guards, and docs-site Forms notes for the independent Toggle Group page. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C67: Kbd Group Composition And Docs Anatomy Cases

Status: completed for this Navigation docs composition pass. This slice adds a `CodexKbdGroup` primitive and expands the Kbd Docs page with Web-style grouped shortcut, button slot, tooltip, and input group addon coverage, each with local inline AXAML source.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexKbd.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Kbd.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/Kbd.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/KbdStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/KbdAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/KbdInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- shadcn Kbd documents `Kbd` and `KbdGroup`, with examples for grouped shortcuts, button slots, tooltip content, input group addons, and RTL.
- Local Kbd already covered single shortcut tokens but used page-local StackPanels for grouped shortcuts, so the missing parity work was a component-owned group primitive and a dedicated anatomy example.

Implementation targets:

- Add `CodexKbdGroup` with size classes, `kbd-group`, `has-items`, and `empty` state classes. Status: completed.
- Add KbdGroup styles with tokenized horizontal grouped spacing and size-aware descendant Kbd density. Status: completed.
- Update default, states, and interaction Kbd examples to use `CodexKbdGroup`, then add `Navigation/KbdAnatomy.axaml` for group, button slot, tooltip content, and input group addon composition. Status: completed.
- Extend structure, state-class, navigation behavior, Docs registration, rendered lifecycle, and docs-site Navigation coverage for grouped shortcut tokens. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C68: Tooltip Provider Trigger Contract And Docs Anatomy

Status: completed for this Overlay interaction-parity pass. This slice moves Tooltip closer to the current shadcn/Radix contract by adding provider and trigger roles, pointer/focus open requests, delay state, side-aware motion, and a dedicated anatomy example with local inline AXAML source.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexTooltip.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Tooltip.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/Tooltip.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/TooltipAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/TooltipInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- shadcn Tooltip documents `TooltipProvider`, `Tooltip`, `TooltipTrigger`, and `TooltipContent`, with side placement, keyboard shortcut content, disabled-button wrapper, focus/hover open behavior, and RTL examples.
- Local Tooltip already had tokenized open/closed side motion but only represented the content surface. The missing Web behavior was trigger-owned open/close routing, provider delay state, and disabled trigger wrapper composition.

Implementation targets:

- Add `CodexTooltipProvider` with delay, skip-delay, disable-hoverable-content, and provider state classes. Status: completed.
- Extend `CodexTooltip` with `Trigger`, `TriggerTemplate`, `OpenDelay`, `CloseDelay`, `HasTrigger`, request-open/request-close timers, pointer/focus routes, Escape dismissal, and detach timer cleanup. Status: completed.
- Update Tooltip styles so closed/open motion applies to the tooltip surface while keeping the trigger visible, and add provider/trigger template parts plus side-aware trigger positioning. Status: completed.
- Add `Overlay/TooltipAnatomy.axaml` covering provider delay, trigger/content, side placement, Kbd content, and disabled trigger wrapper; update default and interaction examples to use trigger composition. Status: completed.
- Extend overlay behavior tests, style guards, Docs static coverage, docs-site overlay notes, rendered lifecycle coverage, and visual fingerprints. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~MotionRenderedLifecycleTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C69: Hover Card Open Change Event And Docs Anatomy

Status: completed for this Overlay interaction/docs parity pass. This slice tightens `CodexHoverCard` around the current shadcn/Radix Hover Card contract: trigger/content composition, controlled open state change notification, side/align placement, delayed open/close state classes, rich preview content, and standalone Docs anatomy coverage.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexHoverCard.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/HoverCard.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/HoverCardAnatomy.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- shadcn Hover Card documents `HoverCard`, `HoverCardTrigger`, and `HoverCardContent`, with open delay, side/align content placement, rich preview content, and examples similar to account/profile previews.
- Local HoverCard already had trigger/content slots, pointer/focus open requests, Escape dismissal, side/align classes, and enter/exit motion, but did not expose an open-change event or dedicated anatomy source case.

Implementation targets:

- Add `CodexHoverCardOpenChangedEventArgs` and `OpenChanged` so root open state changes can be observed like Web `onOpenChange`. Status: completed.
- Add `instant-open`, `delayed-open`, `instant-close`, and `delayed-close` state classes, with AXAML selectors consuming those states. Status: completed.
- Add `Overlay/HoverCardAnatomy.axaml` covering trigger/content, side/align, rich avatar preview, shortcut content, and closed-state composition. Status: completed.
- Register the anatomy case with local inline source expansion, update state/event matrices, docs-site overlay notes, style guards, and visual fingerprints. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~MotionRenderedLifecycleTests|FullyQualifiedName~ComponentStructureTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C57: Item Data Display Component And Docs Cases

Status: completed for this missing-core Data Display pass. This slice adds a `CodexItem` row component and composition primitives that mirror shadcn's Item API, then registers an independent Docs page with inline expandable AXAML under every rendered case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexItem.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Item.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/Item.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ItemStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ItemAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ItemInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- shadcn Item is a versatile row container for media, title, description, actions, and list grouping, with `ItemGroup`, `ItemHeader`, `ItemMedia`, `ItemContent`, `ItemTitle`, `ItemDescription`, `ItemActions`, and `ItemFooter` in the documented composition tree.
- shadcn Item variants are default, outline, and muted; sizes are default, small, and extra small; examples cover icon/avatar/image media, groups, headers, links, and dropdown/action composition.
- The current CodexSwitchUI data-display set had Card, ProviderCard, Table, Carousel, Pagination, ScrollArea, charts, and ImageIcon, but did not expose the generic reusable Item row primitive needed for provider/model/settings lists.

Implementation targets:

- Add `CodexItem` with header, media, title, description, content, actions, footer, variant, size, selected, loading, interactive, command, activation event, and Enter/Space/pointer activation handling. Status: completed.
- Add composition primitives: `CodexItemGroup`, `CodexItemSeparator`, `CodexItemMedia`, `CodexItemHeader`, `CodexItemContent`, `CodexItemTitle`, `CodexItemDescription`, `CodexItemActions`, and `CodexItemFooter`. Status: completed.
- Add `Item.axaml` with row surface, body grid, media slot, title/description/content stack, trailing actions, footer, group surface, separator, media templates, selected/loading/interactive/focus-visible/pressed/disabled states, and tokenized transitions. Status: completed.
- Register Item in theme index, component structure guards, size/variant state tests, navigation/data behavior tests, Docs page registry, state/event matrices, multi-case rendered Docs tests, and visual fingerprints. Status: completed.
- Add independent AXAML samples for default, states, anatomy, and interaction; each appears as an inline expandable `Show code` / `Hide code` block beneath the rendered case. Status: completed.
- Update docs-site Data Display pages in English and Chinese with Item coverage notes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint`
- `npm run build`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A16: Docs Interaction Case Expansion

Status: completed for the next interaction-heavy Docs pass. This slice keeps the focus on Web parity behavior that must be visible and copyable in Docs: popup open state, close-on-select, loading suppression, reduced motion, measured disclosure, keyboard navigation, and composed loading placeholders.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SelectInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SplitButtonInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/SkeletonInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/DropdownButtonInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/CollapsibleInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/PaginationInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- Select, SplitButton, Dropdown, Collapsible, Pagination, and Skeleton already had default and state examples.
- The remaining gap for this group was explicit interaction composition: open popup/surface examples, menu action rows, disabled/loading suppression, reduced-motion fallbacks, and keyboard/page navigation contracts.

Implementation targets:

- Add independent AXAML interaction examples for Select, SplitButton, Skeleton, DropdownButton, Collapsible, and Pagination. Status: completed.
- Register every interaction example as an inline-expandable `DocsExampleCase`. Status: completed.
- Extend static and rendered Docs tests so the new examples are verified under the existing `Show code` workflow. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`

### Agent C54: Menubar Core Component

Status: completed for the next missing Navigation/Web parity component slice. This adds `CodexMenubar` as the persistent command-bar primitive that mirrors the shadcn/Radix Menubar family: root, top-level menus, trigger-owned popup content, checkbox and radio items, grouped content, separators, shortcuts, nested submenus, loading suppression, and orientation-aware keyboard movement.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexMenubar.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexMenuActivation.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Menubar.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/Menubar.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/MenubarStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/MenubarComposition.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/MenubarInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `docs-site/content/docs/en/ui-system/navigation.mdx`
- `docs-site/content/docs/zh/ui-system/navigation.mdx`

Evidence gathered:

- Current shadcn Menubar is backed by the Radix Menubar composition model: persistent root, trigger/content pairs, checkbox items, radio items, submenus, labels/groups/separators, and shortcut rows.
- Radix Menubar keyboard behavior uses Enter/Space and arrow keys to open trigger content, Arrow/Home/End movement across top-level triggers, Escape dismissal, and orientation-aware traversal.
- Existing CodexSwitchUI `CodexMenu`, `CodexContextMenu`, and `CodexDropdownButton` already own activation suppression, submenu timing, popup/light-dismiss styling, focus-visible, shortcuts, and tokenized opacity/transform transitions, so Menubar should reuse that architecture instead of adding a separate menu stack.
- Docs already provide the required per-case workflow through `BuildInlineExample`: each Menubar case renders the component first, then exposes a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that case.

Implementation targets:

- Add `CodexMenubar`, `CodexMenubarMenu`, `CodexMenubarItem`, checkbox/radio item aliases, group, separator, and label primitives. Status: completed.
- Add root open/closed/loading/loop/orientation/size classes plus active menu change events and public open/toggle/dismiss/top-level navigation methods. Status: completed.
- Extend shared menu activation loading suppression so Menubar blocks trigger and leaf activation while loading. Status: completed.
- Add `Menubar.axaml` with Codex-owned templates, `PART_Root`, `PART_ItemRoot`, `PART_Popup`, `PART_MenuSurface`, check/radio indicators, shortcut slot, top-level popup placement, side submenu placement, focus adorner suppression, disabled state, and tokenized opacity/transform transitions. Status: completed.
- Register the style in `ComponentStyles.axaml` and extend high-risk structure, native-item selector, size, and behavior tests so Menubar cannot drift back to default Fluent menu templates. Status: completed.
- Add the independent Docs page `navigation.menubar` plus default, states, composition, and interaction AXAML samples with inline expandable code. Status: completed.
- Update docs-site navigation pages in English and Chinese so public UI-system docs mention Menubar coverage and review criteria. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`: 110 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`: 3 passed.
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`: 1 passed.
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`: 184 passed.
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`: 184 passed.
- `npm run lint` in `docs-site`: passed.
- `npm run build` in `docs-site`: passed.
- `git -C /data/CodexSwitch diff --check`: passed.
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`: passed.

### Agent C51: Calendar Core Component

Status: completed for this missing-core Forms pass. This slice adds `CodexCalendar` as the Avalonia counterpart to shadcn Calendar/DayPicker so the library now covers single-date selection, range selection, outside-day visibility, week numbers, disabled date bounds, booked-date classes, active-day keyboard state, and date-picker-style popover composition.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCalendar.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Calendar.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/Calendar.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/CalendarStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/CalendarComposition.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/CalendarInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn Calendar is described as a component for selecting a date or range of dates and is built on React DayPicker; its usage exposes `mode="single"`, `selected`, and `onSelect`.
- shadcn Calendar examples include basic single selection, range calendar, outside days, week-number display, custom cell sizing, RTL/timezone notes, and Date Picker composition through Popover plus Calendar.
- Current CodexSwitchUI Forms coverage had Select, NativeSelect, InputOtp, Field, ButtonGroup, InputGroup, and other primitives, but no Calendar root component or Docs page.
- Docs already provide the required per-case workflow through `BuildInlineExample`: each rendered example is followed by a local `Show code`/`Hide code` button that expands the exact AXAML sample for that case.

Implementation targets:

- Add `CodexCalendar`, `CodexCalendarDayButton`, `CodexCalendarWeekday`, and `CodexCalendarWeekNumber` with DisplayDate, SelectedDate, RangeStart, RangeEnd, MinDate, MaxDate, BookedDates, ActiveDate, SelectionMode, FirstDayOfWeek, ShowOutsideDays, ShowWeekNumbers, Intent, Size, and previous/next month commands. Status: completed.
- Add pointer and keyboard behavior for day selection, range completion/normalization, disabled-date guards, active-day movement, Home/End row jumps, PageUp/PageDown month changes, and Enter/Space selection. Status: completed.
- Add `Calendar.axaml` with Codex-owned root/day/week templates, focus adorner suppression, focus-visible rings, today/outside/selected/range/booked/unavailable/blank/week-number selectors, intent/size selectors, and tokenized opacity, brush, and transform transitions. Status: completed.
- Register Calendar in `ComponentStyles.axaml` and extend component structure, form behavior, state, Docs layout, rendered lifecycle, and visual fingerprint tests. Status: completed.
- Add Forms Calendar Docs page plus default, states, composition, and interaction AXAML samples; each sample renders first and exposes inline expandable source below the component through the existing Docs example mechanism. Status: completed.
- Update docs-site Forms pages in English and Chinese so public UI-system docs mention Calendar coverage and test expectations. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~ComponentStructureTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`

### Agent C52: Carousel Core Component

Status: completed for this data-display Web parity pass. This slice adds a Codex-owned Carousel so Docs can cover a high-motion shadcn/Web component family with the same per-example source expansion workflow used by the rewritten Docs shell.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCarousel.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Carousel.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/Carousel.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/CarouselStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/CarouselComposition.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/CarouselInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- shadcn Carousel is Embla-style and exposes `Carousel`, `CarouselContent`, `CarouselItem`, `CarouselPrevious`, and `CarouselNext` composition with examples for orientation, options/loop, API/event selection, and keyboard movement.
- Current CodexSwitchUI data display coverage had Card, Table, Pagination, ScrollArea, and charts, but no motion-heavy selected-slide component that exercises previous/next commands and loop boundaries.
- Docs already provide the required per-case workflow through `BuildInlineExample`: render the example component first, then show a `Show code` / `Hide code` button that expands that case's exact standalone AXAML file underneath it.

Implementation targets:

- Add `CodexCarousel` and `CodexCarouselItem` with `SelectedIndex`, `Loop`, `Orientation`, `ShowNavigation`, `ShowStatus`, `SlideCount`, `StatusText`, previous/next commands, `SelectionChanged`, and keyboard navigation. Status: completed.
- Add `Carousel.axaml` with a Codex-owned template, selected slide scale/shadow, previous/next/status controls, horizontal/vertical rail styling, focus-visible ring, disabled opacity, loop/boundary classes, and tokenized motion transitions. Status: completed.
- Register the Carousel style in `ComponentStyles.axaml` and extend structure/state tests so the control cannot drift back to default templates or hard-coded motion. Status: completed.
- Add a categorized Docs page `data.carousel` plus default, states, composition, and interaction AXAML examples. Status: completed.
- Extend Docs tests so Carousel is in the independent page registry, top-level sample list, multi-case inline code gallery, state/event matrices, representative render set, and visual fingerprint page set. Status: completed.
- Update docs-site data-display pages in English and Chinese with Carousel coverage and Web interaction criteria. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git -C /data/CodexSwitch diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C53: Resizable Core Component

Status: completed for this layout Web parity pass. This slice adds a Codex-owned Resizable panel group so the library covers the shadcn/react-resizable-panels composition model with pointer drag, keyboard resize, visible handle grip, orientation, constraints, and per-example inline AXAML source.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexResizable.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Resizable.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/Resizable.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/ResizableStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/ResizableComposition.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/ResizableInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `docs-site/content/docs/en/ui-system/index.mdx`
- `docs-site/content/docs/zh/ui-system/index.mdx`

Evidence gathered:

- Current shadcn Resizable documentation describes accessible resizable panel groups with keyboard support, built on `react-resizable-panels`.
- The wrapper composition is `ResizablePanelGroup`, `ResizablePanel`, and `ResizableHandle`; examples cover horizontal usage, vertical resizing, and `withHandle` for a visible grip.
- Current shadcn docs note the v4 primitive rename to `orientation`, while wrapper names remain unchanged, so CodexSwitchUI should expose Avalonia `Orientation` while keeping the PanelGroup/Panel/Handle mental model.
- CodexSwitchUI had layout shell, sidebar, and section primitives, but no draggable panel group component for editor, sidebar, and inspector layouts.

Implementation targets:

- Add `CodexResizablePanelGroup`, `CodexResizablePanel`, and `CodexResizableHandle` with percentage layout, `DefaultSize`, `MinSize`, `MaxSize`, `PanelSize`, `LayoutSummary`, `LayoutChanged`, and `Size` state. Status: completed.
- Add pointer drag and keyboard paths: horizontal Left/Right, vertical Up/Down, PageUp/PageDown, Home/End, all routed through the same constrained resize method. Status: completed.
- Add `Resizable.axaml` with panel and handle templates, visible `WithHandle` grip, focus-visible ring, dragging state, vertical/horizontal selectors, size selectors, disabled opacity, and tokenized transitions. Status: completed.
- Register the style in `ComponentStyles.axaml` and extend structure/state tests so Resizable cannot lose its style file, template parts, focus adorner suppression, motion, or size selectors. Status: completed.
- Add Docs `layout.resizable` independent page plus default, states, composition, and interaction AXAML samples. Status: completed.
- Extend Docs tests so Resizable is in the independent page registry, top-level sample list, multi-case inline code gallery, state/event matrices, representative render set, and visual fingerprint page set. Status: completed.
- Update docs-site UI overview pages in English and Chinese with Resizable layout coverage and review criteria. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git -C /data/CodexSwitch diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C49: NativeSelect Core Component

Status: completed for this missing-core Forms pass. This slice adds `CodexNativeSelect` as the Avalonia counterpart to shadcn Native Select, keeping it distinct from the custom `CodexSelect` popup while preserving native ComboBox selection, option values, disabled options, optgroup labels, invalid state, focus-visible behavior, and tokenized open motion.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNativeSelect.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/NativeSelect.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexInputGroup.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexButtonGroup.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/InputGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ButtonGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/NativeSelect.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/NativeSelectStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/NativeSelectComposition.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/NativeSelectInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn Native Select documents `NativeSelect`, `NativeSelectOption`, and `NativeSelectOptGroup`, with examples for groups, disabled, invalid, and a clear distinction from the custom Select component.
- Existing `CodexSelect` already covers the custom trigger/content popup path, so NativeSelect should stay as a separate component with native ComboBox selection semantics and a simpler native-select visual contract.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `CodexNativeSelect`, `CodexNativeSelectOption`, and `CodexNativeSelectOptGroup` with option values, placeholder-visible/has-selection classes, invalid class, disabled option classes, optgroup label class, focus-visible handling, and popup-open class. Status: completed.
- Add `NativeSelect.axaml` with Codex-owned template, native option and optgroup templates, disabled/invalid/intent/size selectors, tokenized focus/open transitions, and scoped ComboBoxItem styling. Status: completed.
- Register the style in `ComponentStyles.axaml` and integrate NativeSelect into InputGroup/ButtonGroup composition styling. Status: completed.
- Add Docs `forms.native-select` independent page plus default, states, composition, and interaction AXAML samples; every case renders the component first and exposes its own inline `Show code` toggle. Status: completed.
- Extend static, behavior, docs-layout, rendered lifecycle, and visual fingerprint tests so NativeSelect cannot regress out of style registration, Docs registration, inline code expansion, or cross-theme screenshots. Status: completed.
- Update docs-site Forms pages in English and Chinese to include Native Select coverage and review criteria. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint`
- `npm run build`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C46: ButtonGroup Core Component

Status: completed for the next Forms core-component parity slice. This slice adds a public `CodexButtonGroup` family to cover the current shadcn Button Group surface rather than overloading `CodexToggleGroup`, and expands Docs with inline-code examples for default, state, composition, and interaction coverage.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexButtonGroup.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ButtonGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ButtonGroup*.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn Button Group is a container for related action buttons, not a selection primitive; it exposes `ButtonGroup`, `ButtonGroupSeparator`, and `ButtonGroupText`.
- The current shadcn page documents composition with Button or Input, Separator, and Text, plus accessibility via group semantics and Tab navigation between child controls.
- The current examples include horizontal/vertical orientation, sizes on child buttons, nested groups, separator, split, input, dropdown/select/popover-style composition, and RTL coverage.
- Local `CodexSplitButton` had an internal `PART_ButtonGroup`, but there was no reusable public `CodexButtonGroup` component or Docs page.

Implementation targets:

- Add `CodexButtonGroup`, `CodexButtonGroupText`, and `CodexButtonGroupSeparator` controls with horizontal/vertical classes, variant/size class sync, group item position classes, child button inheritance when group variant/size is explicitly set, separator orientation sync, and automation control-element semantics. Status: completed.
- Add `ButtonGroup.axaml` with tokenized motion, connected horizontal/vertical corners, collapsed outline borders, text segment styling, separator styling, input/select composition hooks, nested group spacing, and disabled group opacity. Status: completed.
- Register `ButtonGroup.axaml` in component styles and extend structure/form tests so the class file, style file, template parts, motion hooks, child classes, separator/text classes, and composition selectors stay protected. Status: completed.
- Add Docs `forms.button-group` independent page plus `ButtonGroup.axaml`, `ButtonGroupStates.axaml`, `ButtonGroupComposition.axaml`, and `ButtonGroupInteraction.axaml`; each uses the existing per-case `Show code`/`Hide code` inline expansion workflow. Status: completed.
- Add rendered lifecycle and visual fingerprint coverage so the new multi-case page renders across Light, Dark, and Custom themes with inline source expanded. Status: completed.
- Update docs-site Forms pages in English and Chinese to mention Button Group coverage and review criteria. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`

### Agent C47: InputGroup Core Component

Status: completed for the next Forms core-component parity slice. This slice adds a public `CodexInputGroup` family for the current shadcn Input Group surface and expands Docs with inline-code examples for default, state, composition, and interaction coverage.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexInputGroup.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/InputGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/InputGroup*.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn Input Group exposes wrapper, input/textarea, addon, button, and text pieces; addons can align inline-start, inline-end, block-start, and block-end.
- Web behavior keeps focus on the child input/control while the group exposes a focus-within visual ring.
- Button and select composition should remain child-owned: loading buttons suppress their own clicks, select popups keep native selected/open state, and disabled child controls stay visible but inactive.
- Docs already provide the required per-case workflow through `BuildInlineExample`: rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `CodexInputGroup`, `CodexInputGroupAddon`, `CodexInputGroupText`, `CodexInputGroupButton`, `CodexInputGroupInput`, and `CodexInputGroupTextarea` with size/intent class sync, inline/block addon align classes, child item position classes, focus-within class handling, and child size/intent propagation. Status: completed.
- Add `InputGroup.axaml` with a shared border/focus ring, tokenized motion, transparent child input/select/textarea composition, addon surface styling, loading-capable ghost action buttons, and inline/block layout selectors. Status: completed.
- Register `InputGroup.axaml` in component styles and extend structure/form tests so template parts, motion hooks, focus-within styling, addon align selectors, control selectors, and child propagation stay protected. Status: completed.
- Add Docs `forms.input-group` independent page plus `InputGroup.axaml`, `InputGroupStates.axaml`, `InputGroupComposition.axaml`, and `InputGroupInteraction.axaml`; each uses the per-case inline AXAML expansion workflow. Status: completed.
- Add rendered lifecycle and visual fingerprint coverage so the new multi-case page renders across Light, Dark, and Custom themes with inline source expanded. Status: completed.
- Update docs-site Forms pages in English and Chinese to mention Input Group coverage and review criteria. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git -C /data/CodexSwitch diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C48: Label Core Component

Status: completed for the next Forms core-component parity slice. This slice adds a public `CodexLabel` wrapper around Avalonia's target-aware Label behavior so CodexSwitchUI now covers shadcn/Radix Label rather than relying only on page-local text blocks or `CodexField.Label` strings.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexLabel.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexField.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Label.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Field.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/Label*.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn Label renders an accessible label associated with controls, and the docs direct richer form layouts through Field/FieldLabel.
- Avalonia `Label` already focuses its `Target` on pointer click or access-key press, matching the key Web association event path more directly than a custom TextBlock.
- Local `CodexField` previously rendered `PART_Label` as a `TextBlock`, so field labels had visual parity but did not share the reusable Label component contract.

Implementation targets:

- Add `CodexLabel` with intent/size/required classes, target presence class, target-disabled class, target-aware cursor styling, required marker template part, access-key recognition, and tokenized foreground/opacity transitions. Status: completed.
- Update `CodexField` so its `PART_Label` is a `CodexLabel`, preserving required marker and intent styling while sharing the same label component surface. Status: completed.
- Register `Label.axaml` in component styles and extend structure/form tests so template parts, access-key support, target classes, required marker, intent/size selectors, and Field reuse remain protected. Status: completed.
- Add Docs `forms.label` independent page plus `Label.axaml`, `LabelStates.axaml`, `LabelComposition.axaml`, and `LabelInteraction.axaml`; each uses the per-case inline AXAML expansion workflow. Status: completed.
- Add rendered lifecycle and visual fingerprint coverage so the new multi-case page renders across Light, Dark, and Custom themes with inline source expanded. Status: completed.
- Update docs-site Forms pages in English and Chinese to mention Label coverage and review criteria. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C42: Accordion Core Component

Status: completed for the first Accordion parity pass. This slice fills a shadcn/Radix core navigation gap with a Codex-owned Accordion root and item pair, preserving Web-style disclosure motion and trigger events while fitting the existing Semi.Avalonia-like style-index architecture.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexAccordion.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCollapsible.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Accordion.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/Accordion*.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `docs-site/content/docs/*/ui-system/navigation.mdx`

Evidence gathered:

- Radix Accordion exposes Root, Item, Header, Trigger, and Content parts, supports single or multiple open items, optional single-mode collapsing, horizontal/vertical trigger navigation, and content size animation through measured content height.
- shadcn lists Accordion as a core navigation component with `type="single" | "multiple"`, `collapsible`, `value`, `onValueChange`, Space/Enter activation, Arrow/Home/End trigger navigation, and animated content.
- Semi.Avalonia's documentation reinforces the architecture direction used here: component styles and variant resources are indexed and theme-owned instead of being page-local visual hacks.

Implementation targets:

- Add `CodexAccordion` and `CodexAccordionItem` with `Type`, `IsCollapsible`, `Orientation`, `Size`, `AnimationDuration`, `OpenValues`, and `ValueChanged`. Status: completed.
- Reuse `CodexCollapsible` measured-height animation through virtual trigger hooks while letting Accordion root coordinate single/multiple item state. Status: completed.
- Add `Accordion.axaml` with Codex-owned root and item templates, `PART_Trigger`, `PART_Chevron`, measured content clip, open/closed classes, focus-visible selectors, disabled opacity, size states, and tokenized transitions. Status: completed.
- Register Accordion in `ComponentStyles.axaml` and structure tests so it cannot silently fall back to default styles. Status: completed.
- Add a Navigation/Accordion Docs page with default, states, anatomy, and interaction AXAML samples, each using the local inline `Show code` expansion. Status: completed.
- Update docs-site navigation pages in English and Chinese to include Accordion in public UI-system coverage. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C43: AlertDialog Core Component

Status: completed for the first AlertDialog parity pass. This slice fills the interruptive confirmation overlay gap with a Codex-owned alert dialog that inherits the existing dialog dismissal and focus-return foundation while adding Radix/shadcn response-required defaults.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexAlertDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/AlertDialog.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/AlertDialog*.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `docs-site/content/docs/*/ui-system/overlay.mdx`

Evidence gathered:

- Radix Alert Dialog is a modal, response-required overlay with title, description, cancel/action slots, Escape dismissal, focus restoration, and least-destructive initial focus.
- shadcn Alert Dialog exposes Header, Title, Description, Footer, Cancel, Action, destructive examples, and compact/semantic action composition.
- The existing Codex Dialog foundation already owns Escape, outside-pointer, close command, open/closed classes, and restore-focus behavior, so AlertDialog should extend that path instead of creating a second overlay primitive.

Implementation targets:

- Add `CodexAlertDialog` with cancel/action content, commands, loading states, `ActionVariant`, `Size`, close-on flags, media slot, and least-destructive focus behavior. Status: completed.
- Default `IsCloseVisible=false`, `DismissOnOutsidePointer=false`, and `FocusCancelOnOpen=true` to match response-required Web behavior. Status: completed.
- Add `AlertDialog.axaml` with `PART_Surface`, header/media/title/description/content/footer, `PART_Cancel`, `PART_Action`, destructive media tone, size states, loading state, closed transform, and tokenized transitions. Status: completed.
- Register AlertDialog in `ComponentStyles.axaml` and structure/state/rendered lifecycle tests so style registration, commands, focus, outside-pointer policy, and docs rendering cannot regress silently. Status: completed.
- Add an Overlay/AlertDialog Docs page with default, states, anatomy, and interaction AXAML samples, each using the local inline `Show code` expansion. Status: completed.
- Update docs-site overlay pages in English and Chinese to include AlertDialog in public UI-system coverage. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~OverlayRenderedLifecycleTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C44: Breadcrumb Core Component

Status: completed for the first Breadcrumb parity pass. This slice fills a shadcn core navigation gap with a Codex-owned breadcrumb component family that keeps links, current page, separators, and collapsed ellipsis pieces independently composable.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexBreadcrumb.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Breadcrumb.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/Breadcrumb*.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/*/ui-system/navigation.mdx`

Evidence gathered:

- shadcn Breadcrumb displays the path to the current resource and exposes `Breadcrumb`, `BreadcrumbList`, `BreadcrumbItem`, `BreadcrumbLink`, `BreadcrumbPage`, `BreadcrumbSeparator`, and `BreadcrumbEllipsis`.
- shadcn examples include basic paths, custom separators, dropdown composition, collapsed trails, custom link composition, and RTL usage.
- WAI-ARIA APG describes breadcrumbs as a labelled navigation landmark containing ordered parent links, with no special keyboard interaction and current-page semantics for the current link/page.
- Semi.Avalonia's architecture guidance keeps brushes, dimensions, control themes, and style classes resource-driven, so Breadcrumb styling follows the existing Codex theme include and dynamic-resource pattern.

Implementation targets:

- Add `CodexBreadcrumb`, `CodexBreadcrumbList`, `CodexBreadcrumbItem`, `CodexBreadcrumbLink`, `CodexBreadcrumbPage`, `CodexBreadcrumbSeparator`, and `CodexBreadcrumbEllipsis`. Status: completed.
- Implement Web-like link behavior where ancestor links can execute host commands while `IsCurrent` links suppress activation. Status: completed.
- Add navigation and ellipsis automation names for the labelled breadcrumb path and collapsed-state affordance. Status: completed.
- Add `Breadcrumb.axaml` with root/list/item/link/page/separator/ellipsis templates, focus-visible link styling, pointer/pressed feedback, current-page treatment, disabled opacity, size states, and tokenized transitions. Status: completed.
- Register Breadcrumb in `ComponentStyles.axaml` and add structure/state tests so the component cannot silently fall back to default control styles. Status: completed.
- Add a Navigation/Breadcrumb Docs page with default, states, anatomy, and interaction AXAML samples, each using the local inline `Show code` expansion. Status: completed.
- Update docs-site navigation pages in English and Chinese to include Breadcrumb in public UI-system coverage. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~NavigationDataComponentTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent A19: Docs Registered-Page Visual Fingerprint Expansion

Status: completed for every currently registered Docs menu page. This slice moves visual regression coverage from multi-case pages only to the real `MainWindow` page registry, so every independent menu page must render, expand its local inline code examples, and match a comparable Light/Dark/Custom visual fingerprint.

Scope:

- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- `MainWindow` currently registers 55 independent Docs pages.
- The comparable visual baseline covered 57 signatures after Agent A18, which represented 19 multi-case pages across three themes.
- Single-case pages such as EmptyState, ScrollArea, UsageTrendChart, ImageIcon, primitives, layout pages, and several navigation/feedback/data pages still lacked comparable fingerprint coverage.

Implementation targets:

- Replace the static visual page subset with a reflection-backed `AllRegisteredPageIds()` reader over `MainWindow.Categories`. Status: completed.
- Add a static guard that requires the visual fingerprint test to track the actual registered page list. Status: completed.
- Regenerate `DocsVisualFingerprints.json` so all 55 registered pages have Light, Dark, and Custom expanded-code fingerprints. Status: completed with 165 signatures.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`

### Agent A20: Overlay Runtime Motion And Trigger Assertions

Status: completed for the first overlay runtime-motion assertion pass. This slice moves beyond final screenshots for Tooltip, Popover, and CommandDialog by verifying mounted transition durations, open/closed classes, Escape dismissal, reduced-motion resolution, and loading suppression for close-on-select behavior.

Scope:

- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexTooltip.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexPopover.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCommandDialog.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Tooltip.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Popover.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/CommandDialog.axaml`

Evidence gathered:

- Existing rendered motion tests already covered table refresh, collapsible, sonner, skeleton, usage pie chart, hover-card delay, and scroll-area visibility motion.
- Tooltip, Popover, and CommandDialog had tokenized opacity/transform transitions in styles and open/closed/loading classes in controls, but lacked mounted runtime assertions proving those transitions resolve and behavior paths close or suppress correctly.

Implementation targets:

- Add mounted runtime assertions for nonzero Tooltip/Popover/CommandDialog opacity and render-transform transitions under normal motion. Status: completed.
- Add reduced-motion assertions proving those same transitions resolve to zero duration. Status: completed.
- Verify Escape dismissal for Tooltip and Popover, and verify CommandDialog loading suppresses close-on-select while normal command item selection dismisses. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~MotionRenderedLifecycleTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`

### Agent A21: Dropdown And Split Button Popup Motion Assertions

Status: completed for the first native popup trigger runtime-motion pass. This slice extends A20 to DropdownButton and SplitButton, covering popup surface motion, chevron motion, loading suppression, close-on-select, primary action suppression, and focus restoration in mounted headless rendering.

Scope:

- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDropdownButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSplitButton.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/DropdownButton.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/SplitButton.axaml`

Evidence gathered:

- DropdownButton and SplitButton already exposed open/closed/loading/focus-restore state and tokenized popup surface transitions.
- Mounted runtime testing showed popup surface transitions resolved correctly, but chevron `PathIcon#PART_Chevron` transitions were not applied because the selector used `/template/` while the icon lives inside a `CodexButton` content slot.

Implementation targets:

- Add mounted runtime assertions for DropdownButton and SplitButton popup surface opacity/transform transitions, chevron transform transitions, open/closed classes, loading suppression, close-on-select, primary action execution/suppression, and restore-focus behavior. Status: completed.
- Add reduced-motion assertions proving popup surface and chevron transition durations resolve to zero. Status: completed.
- Fix DropdownButton and SplitButton chevron selectors so content-slot chevrons receive open rotation and tokenized transform transitions. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~MotionRenderedLifecycleTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`

### Agent A22: Chevron Disclosure Runtime Motion Assertions

Status: completed for Select, NavigationMenu, and Collapsible mounted chevron/open-motion verification. This slice follows the A21 selector finding and proves the remaining high-risk chevron disclosure controls actually receive tokenized transform/opacity transitions in their mounted templates and reduced-motion mode.

Scope:

- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSelect.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexNavigationMenu.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCollapsible.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Select.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/NavigationMenu.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Collapsible.axaml`

Evidence gathered:

- A21 found that a chevron selector can look correct in source but miss when the icon is placed through a content slot.
- Select, NavigationMenu, and Collapsible use chevrons and open-state motion, but their mounted template coverage did not yet prove those selector paths and reduced-motion durations at runtime.

Implementation targets:

- Add mounted normal-motion assertions for Select dropdown-open chevron, NavigationMenu viewport/indicator/item chevron, and Collapsible disclosure chevron. Status: completed.
- Add mounted reduced-motion assertions proving those chevron, viewport, and indicator transitions resolve to zero duration. Status: completed.
- Harden the shared opacity-transition assertion helper so it verifies the `Opacity` transition by property when a control has multiple `DoubleTransition` entries such as width and min-height. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~MotionRenderedLifecycleTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`

### Agent A23: Menu And ContextMenu Submenu Runtime Motion Assertions

Status: completed for Menu/ContextMenu submenu arrow, popup-surface transition, reduced-motion, and pointer delay scheduling coverage. This slice extends the mounted runtime motion checks to the native menu family and fixes selector paths that looked correct in source but did not hit `CodexMenuItem`/`CodexContextMenuItem` templates at runtime.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Menu.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ContextMenu.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MenuRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`

Evidence gathered:

- Mounted runtime testing showed `PART_SubMenuArrow` transitions were null for Menu/ContextMenu submenu items, proving the old `/template/ PathIcon#PART_SubMenuArrow` selectors were not reaching the nested `Panel > Border > Grid` template structure on the concrete Codex item types.
- The same selector shape was also high risk for submenu popup surfaces, open/hover/focus styles, checked/radio icons, placement-specific transforms, size variants, and disabled states.
- DispatcherTimer-based pointer delays do not reliably advance by sleeping in the headless test harness, so the behavior test verifies the default delayed path schedules `_openTimer`/`_closeTimer` without immediate state changes, then uses zero-delay requests to prove the same activation path opens/closes mounted submenu state.

Implementation targets:

- Retarget Menu and ContextMenu submenu selectors to `CodexMenuItem`/`CodexContextMenuItem` with the concrete template path through `Panel Border#PART_ItemRoot` and `Panel Popup#PART_Popup Border#PART_SubMenuSurface`. Status: completed.
- Add mounted normal-motion assertions for submenu arrow opacity/transform transitions and submenu surface opacity/transform transitions. Status: completed.
- Add mounted reduced-motion assertions proving submenu arrow and submenu surface transition durations resolve to zero. Status: completed.
- Add mounted pointer-delay scheduling assertions for Menu and ContextMenu submenu open/close requests, plus immediate zero-delay state transitions. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~MenuRenderedLifecycleTests|FullyQualifiedName~MotionRenderedLifecycleTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A24: Menu And ContextMenu Docs Interaction Cases

Status: completed for a focused Docs coverage pass on Menu and ContextMenu interaction examples. This slice adds independent AXAML samples and inline-preview registrations for the Web-parity behaviors proven in A23: keyboard submenu open/close, pointer delay semantics, loading suppression, side-aware submenu placement, checked/radio selection, and disabled leaf rows.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/MenuInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/ContextMenuInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- `navigation.menu` and `navigation.context-menu` already had default, state, and anatomy samples, but did not yet have dedicated interaction pages showing the event-trigger contracts fixed and tested in A23.
- Docs already support the required per-case workflow: each `DocsExampleCase` renders the component first, then exposes a local `Show code` button that expands the exact AXAML sample beneath the rendered case.
- Adding new inline examples changes expanded Docs screenshots, so the registered-page visual fingerprint baseline must be refreshed through the existing update path and immediately rechecked without the update flag.

Implementation targets:

- Add `Navigation/MenuInteraction.axaml` with keyboard submenu, pointer delay, loading gate, checked/radio, and disabled leaf examples. Status: completed.
- Add `Navigation/ContextMenuInteraction.axaml` with right/left/top/bottom side placement, open submenu surfaces, loading gate, checked/radio, inset, and disabled leaf examples. Status: completed.
- Register both examples on their owning component pages with preview builders and `Show code` inline expansion. Status: completed.
- Extend Docs static sample guards so the new interaction samples remain registered and present on disk. Status: completed.
- Refresh registered-page visual fingerprints after the Docs page content changed. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A25: Tabs And NavigationMenu Docs Interaction Cases

Status: completed for the next navigation Docs interaction pass. This slice adds independent AXAML samples and inline-preview registrations for the event contracts that make Tabs and NavigationMenu feel Web-like: roving keyboard selection, Home/End traversal, Enter/Space activation, disabled-skip behavior, pointer/focus activation, viewport open/close, motion direction, vertical orientation, and Escape dismissal.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/TabsInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/NavigationMenuInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- `navigation.tabs` already had default, state, and anatomy examples, but did not yet expose a dedicated interaction example for Arrow/Home/End and Enter/Space behavior.
- `navigation.navigation-menu` had default and state examples only, leaving pointer/focus activation, motion direction, Escape close, and vertical trigger traversal under-documented in the inline-code gallery.
- The existing rendered Docs test expands every local `Show code` button across themes, so registering these examples proves they appear beneath the rendered component and load the exact AXAML source instead of a missing-sample placeholder.

Implementation targets:

- Add `Navigation/TabsInteraction.axaml` with horizontal roving, line variant, vertical roving, activation, and disabled-skip cases. Status: completed.
- Add `Navigation/NavigationMenuInteraction.axaml` with pointer-enter activation, next/previous motion, vertical trigger traversal, disabled trigger, and Escape dismissal cases. Status: completed.
- Register both examples on their owning component pages with matching preview builders. Status: completed.
- Extend Docs static guards so the new interaction samples remain registered and present on disk. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A26: SideNav And SegmentedControl Docs Interaction Cases

Status: completed for another navigation Docs interaction pass. This slice adds independent AXAML samples and inline-preview registrations for SideNav sibling selection and SegmentedControl moving-indicator behavior, keeping Docs aligned with the Web event model instead of showing only static selected states.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SideNavInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SegmentedControlInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- `CodexSideNavItem.OnClick` selects itself and clears sibling `CodexSideNavItem` selection, but the Docs page only showed default and static state examples.
- `CodexSegmentedButton.OnClick` selects itself, clears sibling selected state, and queues `CodexSegmentedControl` indicator remeasure, but the Docs page only showed the selected indicator as a static state.
- The existing rendered Docs test expands every local `Show code` button across themes, so registering these cases proves they display beneath the rendered component and load the exact AXAML sample.

Implementation targets:

- Add `Navigation/SideNavInteraction.axaml` with pointer/keyboard selection, sibling clearing, disabled rows, detail alignment, and no-icon rows. Status: completed.
- Add `Navigation/SegmentedControlInteraction.axaml` with sibling selection, indicator remeasure, disabled segments, time-range density, and externally controlled selection examples. Status: completed.
- Register both examples on their owning component pages with matching preview builders. Status: completed.
- Extend Docs static guards so the new interaction samples remain registered and present on disk. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A27: Command Docs Interaction Case

Status: completed for the Command Docs interaction pass. This slice adds a dedicated inline-expandable Command example for the event and state paths that make the command palette match the Web contract: sibling active selection, loading suppression, focus-visible input, empty result display, disabled result display, and shortcut/icon slots.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/CommandInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- `CodexCommandItem.OnClick` suppresses activation while nested under a loading `CodexCommand`; otherwise it activates and clears sibling active items under the same logical parent.
- `CodexCommandInput` and `CodexCommandItem` both use the shared `CodexFocusVisible` pseudo-class path.
- The rendered Docs test expands every local `Show code` button across themes, so registering this case proves the source appears below the rendered component and loads the exact AXAML sample.

Implementation targets:

- Add `Navigation/CommandInteraction.axaml` with sibling selection, loading gate, focus input, empty state, disabled result, shortcut, and icon examples. Status: completed.
- Register the new example under `navigation.command` with a matching `BuildCommandInteractionPreview` builder. Status: completed.
- Extend Docs static guards so the interaction sample remains registered and present on disk. Status: completed.
- Refresh the command expanded-page visual fingerprint after the Docs page content changed. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A28: Forms Toggle And Range Docs Interaction Cases

Status: completed for a focused Forms interaction Docs pass. This slice adds independent inline-expandable examples for Checkbox, Radio, Switch, and Slider so the core form controls show their Web-style event contracts instead of only static checked/value states.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/CheckboxInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/RadioInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SwitchInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/SliderInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- `CodexCheckBox`, `CodexRadio`, `CodexSwitch`, and `CodexSlider` all preserve native Avalonia interaction paths while adding the shared `CodexFocusVisible` pseudo-class contract.
- Pointer press suppresses the focus-visible ring on these controls, while keyboard or programmatic focus can expose it.
- Checkbox and Radio templates animate glyph/dot opacity and scale; Switch animates thumb position and pressed scale; Slider animates thumb size/scale and track/brush state.
- Existing Docs pages had states for these controls, but Checkbox, Radio, Switch, and Slider did not all have dedicated interaction examples with inline code expansion coverage.

Implementation targets:

- Add `Forms/CheckboxInteraction.axaml` with pointer toggle, Space activation, three-state cycle, focus-visible target, sizes, intents, and disabled guard examples. Status: completed.
- Add `Forms/RadioInteraction.axaml` with sibling selection, keyboard traversal, intent feedback, and disabled group examples. Status: completed.
- Add `Forms/SwitchInteraction.axaml` with pointer toggle, keyboard activation, thumb motion, content, size, intent, and disabled guard examples. Status: completed.
- Add `Forms/SliderInteraction.axaml` with pointer drag, keyboard range changes, vertical orientation, intent, and disabled guard examples. Status: completed.
- Register the new examples under their owning component pages with matching preview builders. Status: completed.
- Extend static and rendered Docs guards so the new interaction samples remain registered, present on disk, and expanded under each component across themes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A29: Text Input Docs Interaction Cases

Status: completed for the next Forms input interaction pass. This slice adds dedicated inline-expandable examples for TextBox and Textarea so single-line and multiline input behavior is represented by standalone AXAML samples, not only static validation states.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/TextBoxInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/TextareaInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- `CodexTextBox` adds the shared `CodexFocusVisible` pseudo-class on keyboard/programmatic focus and suppresses it on pointer press.
- `CodexTextBox` syncs intent, size, and read-only classes; its template owns placeholder visibility, caret brush, selection brush, left/right slots, and focus-ring transitions.
- `CodexTextarea` inherits the TextBox focus and validation contract while enabling multiline entry, wrapping, and owned vertical scroll behavior.
- TextBox had multi-case Docs coverage but no dedicated interaction example; Textarea had states but was not yet covered by the multi-case inline-code render test.

Implementation targets:

- Add `Forms/TextBoxInteraction.axaml` with keyboard focus, pointer focus, selection/caret, left/right slot content, validation feedback, read-only, and disabled examples. Status: completed.
- Add `Forms/TextareaInteraction.axaml` with multiline entry, wrapping, placeholder, scroll, validation feedback, read-only, and disabled examples. Status: completed.
- Register both examples under their owning component pages with matching preview builders. Status: completed.
- Extend static and rendered Docs guards so both samples remain registered, present on disk, and expanded under each component across themes. Status: completed.
- Refresh Docs visual fingerprints after the current working tree rendered `feedback.avatar:expanded` differently from the previous baseline. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A30: Feedback Loading Motion And Action Interaction

Status: completed for a feedback loading and action pass. This slice fixes a real Web parity gap in `CodexProgress` indeterminate loading, then adds independent inline-expandable Docs examples for EmptyState, Spinner, and Progress lifecycle/interaction behavior.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexProgress.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Progress.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/EmptyStateInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/SpinnerInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/ProgressInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/MotionRenderedLifecycleTests.cs`

Evidence gathered:

- `CodexEmptyState` routes primary and secondary template button clicks through `TryExecuteAction` / `TryExecuteSecondaryAction`, and suppresses both paths when disabled, loading, or command `CanExecute` is false.
- `CodexSpinner` already starts a runtime timer on attach, stops it on detach, exposes active/paused classes, and maps active state to automation item status.
- `CodexProgress` previously marked indeterminate state with a class and static indicator, but did not move the indeterminate indicator; this fell short of the Web loading affordance.

Implementation targets:

- Add `IndeterminateAnimationDuration`, `IndeterminateIndicatorWidth`, and `IndeterminateIndicatorMargin` to `CodexProgress`, with a timer-driven eased loop while attached, enabled, indeterminate, and motion duration is non-zero. Status: completed.
- Bind the progress template indeterminate segment to the animated width and margin, and use the theme `CodexSwitch.SkeletonShimmerDuration` resource so reduced motion resolves to a static frame. Status: completed.
- Add `Feedback/EmptyStateInteraction.axaml` with action request, loading gate, disabled surface, and semantic recovery examples. Status: completed.
- Add `Feedback/SpinnerInteraction.axaml` with attach animation, paused state, reduced-motion static frame, and composed loading-row examples. Status: completed.
- Add `Feedback/ProgressInteraction.axaml` with determinate transition, indeterminate loop, reduced-motion static frame, and disabled guard examples. Status: completed.
- Register the new examples under their owning component pages with matching preview builders, and extend static/rendered Docs guards. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~MotionRenderedLifecycleTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A31: Data Display Interaction Docs Cases

Status: completed for the next Data Display interaction pass. This slice adds dedicated inline-expandable Docs examples for ProviderCard, PinnedTable, and ScrollArea so the Data Display category no longer relies only on static state/anatomy coverage for its event-heavy surfaces.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ProviderCardInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/PinnedTableInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ScrollAreaInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- `CodexProviderCard` owns sibling active selection in `OnClick`, plus active/dragging classes and slot presence flags for leading, icon, meta, description, status, usage, and actions.
- `CodexScrollArea` owns scroll metrics, boundary classes, hover/scroll visibility classes, and an idle timer that clears `IsScrolling` after scroll activity.
- `CodexPinnedTable` owns horizontal body-to-header scroll synchronization and `TransitionKey` refresh motion across start, middle, and end regions.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `DataDisplay/ProviderCardInteraction.axaml` with sibling active selection, drag feedback, action/slot composition, and disabled guard examples. Status: completed.
- Add `DataDisplay/PinnedTableInteraction.axaml` with synchronized horizontal scroll, refresh action, density toggle, and transition-key examples. Status: completed.
- Add `DataDisplay/ScrollAreaInteraction.axaml` with scroll metrics, top/bottom boundary actions, hover visibility, scroll visibility, idle reset, and disabled guard examples. Status: completed.
- Register all three examples under their owning component pages with matching preview builders. Status: completed.
- Extend static and rendered Docs guards so these Data Display interaction samples stay registered, present on disk, and expanded under each component across themes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A32: Usage Chart Interaction Docs Cases

Status: completed for the chart interaction Docs pass. This slice adds dedicated inline-expandable examples for UsagePieChart and UsageTrendChart so chart hover, refresh, reduced-motion, granularity, and empty-state behavior is represented by standalone AXAML samples instead of only default/state examples.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/UsagePieChartInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/UsageTrendChartInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- `CodexUsagePieChart` owns pointer hover detection, slice emphasis, tooltip position interpolation, collection refresh draw animation, compact state, empty state, and reduced-motion resolution through `AnimationDuration`.
- `CsUsageTrendChart` owns pointer marker/tooltip behavior, `ItemsSource` refresh animation, `Granularity` axis rebuilds, `IsRefreshing` overlay animation, and chart-owned empty plot rendering.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `DataDisplay/UsagePieChartInteraction.axaml` with hover/tooltip, data refresh, compact density, reduced-motion, and empty/compact guard examples. Status: completed.
- Add `DataDisplay/UsageTrendChartInteraction.axaml` with pointer tooltip, refresh overlay, granularity switch, series refresh, and empty-state examples. Status: completed.
- Register both examples under their owning component pages with matching preview builders and reusable chart sample data. Status: completed.
- Extend static and rendered Docs guards so these chart interaction samples stay registered, present on disk, and expanded under each component across themes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~MotionRenderedLifecycleTests|FullyQualifiedName~EChartsUsageTrendChartTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A33: Forms Button And Field Interaction Docs Cases

Status: completed for the next Forms interaction pass. This slice adds dedicated inline-expandable examples for Button, IconButton, and Field so foundational action controls and field validation behavior are represented by standalone AXAML samples, not only state/anatomy examples.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ButtonInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/IconButtonInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/FieldInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- `CodexButton` owns Web-style pointer/keyboard activation through the native button path, suppresses activation while `IsLoading`, syncs loading/icon slot classes, and exposes focus-visible only for keyboard/programmatic focus.
- `CodexIconButton` inherits the button activation/loading/focus contract while adding fixed icon sizing and the round geometry class.
- `CodexField` owns label, description, message, required marker, intent, size, and slot presence classes while child controls keep their own focus-visible, disabled, and validation behavior.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `Forms/ButtonInteraction.axaml` with activation, focus-visible, loading suppression, intent actions, icon slots, and disabled guard examples. Status: completed.
- Add `Forms/IconButtonInteraction.axaml` with toolbar activation, round geometry, loading suppression, destructive action, and disabled guard examples. Status: completed.
- Add `Forms/FieldInteraction.axaml` with validation message updates, required marker, child focus targets, multiline child content, and disabled child guard examples. Status: completed.
- Register all three examples under their owning component pages with matching preview builders. Status: completed.
- Extend static and rendered Docs guards so these Forms interaction samples stay registered, present on disk, and expanded under each component across themes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A34: Overlay Dialog And Popover Interaction Docs Cases

Status: completed for the next Overlay interaction pass. This slice adds dedicated inline-expandable examples for Dialog and Popover so dismissal, focus restoration, Escape/outside-pointer policy, action slots, and closed exit state are represented by standalone AXAML samples, not only static state/anatomy examples.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DialogInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/PopoverInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`

Evidence gathered:

- `CodexDialog` owns `DismissCommand`, `CloseOnEscape`, `DismissOnOutsidePointer`, `RestoreFocusElement`, `RestoreFocusRequested`, `IsOpen`, open/closed classes, and slot presence classes for header, content, action, and close content.
- `CodexPopover` mirrors the same dismissal and focus-return contract for anchored surfaces, with host-controlled open state and manual policy toggles.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `Overlay/DialogInteraction.axaml` with dismiss command, restore-focus target, manual Escape/outside-pointer policy, closed exit state, and action close surface examples. Status: completed.
- Add `Overlay/PopoverInteraction.axaml` with trigger-owned open state, dismiss command, restore-focus target, persistent manual policy, closed exit state, and action slot examples. Status: completed.
- Register both examples under their owning component pages with matching preview builders. Status: completed.
- Extend static Docs guards so these Overlay interaction samples stay registered and present on disk, while existing multi-case rendered guards expand them under each component across themes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~OverlayRenderedLifecycleTests|FullyQualifiedName~OverlayFeedbackComponentTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A35: Feedback Toast And Alert Interaction Docs Cases

Status: completed for the next Feedback interaction pass. This slice adds independent inline-expandable examples for Alert and Toast so slotted action clicks, dynamic slot presence, dismiss command routing, close-command callbacks, manual Escape policy, reopen toggles, and closed exit state are represented by standalone AXAML samples below the owning component.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/AlertInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/ToastInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- `CodexAlert` owns title, description, icon, content, action, variant, and slot-presence classes, so the interaction docs should exercise action slot activation, rich content composition, disabled action controls, and dynamic description updates.
- `CodexToast` owns `DismissCommand`, `CloseCommand`, `CloseOnEscape`, `IsOpen`, `IsCloseVisible`, action and close slots, plus `open` / `closed` classes, so the docs should show the converged dismiss path and host-controlled manual policy.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `Feedback/AlertInteraction.axaml` with slotted action event, rich content action, disabled action guard, and dynamic slot presence examples. Status: completed.
- Add `Feedback/ToastInteraction.axaml` with dismiss command path, close-command callback surface, manual Escape policy, action-without-dismissal, and closed exit examples. Status: completed.
- Register both examples under their owning component pages with matching preview builders. Status: completed.
- Extend static and rendered Docs guards so these Feedback interaction samples stay registered, present on disk, and expanded under each component across themes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~OverlayFeedbackComponentTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A36: Feedback Badge And Avatar Interaction Docs Cases

Status: completed for the next Feedback dynamic-state pass. This slice adds independent inline-expandable examples for Badge and Avatar so host-driven property updates, status dot changes, count/fallback replacement, size transitions, and disabled host composition are represented by standalone AXAML samples below the owning component.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/BadgeInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/AvatarInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- `CodexBadge` owns variant, size, status variant, and status visibility classes, so Docs should show host events changing status, counts, and density without replacing badge chrome.
- `CodexAvatar` owns fallback, optional image presence, size, variant, status variant, and status visibility classes, so Docs should show presence changes, fallback replacement, density changes, and composition inside command hosts.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `Feedback/BadgeInteraction.axaml` with host-driven status, route count update, density toggle, and disabled host composition examples. Status: completed.
- Add `Feedback/AvatarInteraction.axaml` with presence status update, fallback replacement, density toggle, grouped identity, and disabled host composition examples. Status: completed.
- Register both examples under their owning component pages with matching preview builders. Status: completed.
- Extend static and rendered Docs guards so Badge and Avatar now join the multi-case inline-code render matrix across themes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~OverlayFeedbackComponentTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A37: Data Display Card And Table Interaction Docs Cases

Status: completed for the next Data Display interaction pass. This slice adds independent inline-expandable examples for Card and Table so pointer-driven card selection, footer actions, dynamic slot presence, row selection, sibling clearing, density toggles, hover guards, transition refresh, disabled row behavior, and footer alignment are represented by standalone AXAML samples below the owning component.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/CardInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/TableInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- `CodexCard` owns interactive state and slot-presence classes for title, description, content, and footer, so Docs should show pointer interaction, footer action updates, dynamic slot changes, and disabled action composition.
- `CodexTable` owns hoverable, striped, compact, and transition-key classes while `CodexTableRow` owns selected state, so Docs should show row selection, sibling clearing, disabled row guards, density/hover toggles, and transition refresh without replacing the table scaffold.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `DataDisplay/CardInteraction.axaml` with interactive card event, dynamic slots, disabled action guard, and dense composition examples. Status: completed.
- Add `DataDisplay/TableInteraction.axaml` with selectable rows, loading refresh surface, disabled selection guard, and alignment/footer examples. Status: completed.
- Register both examples under their owning component pages with matching preview builders. Status: completed.
- Extend static and rendered Docs guards so Card joins the multi-case inline-code render matrix and Table checks the new interaction sample. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~NavigationDataComponentTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A38: Data Display Metric ImageIcon RankedBar Interaction Docs Cases

Status: completed for the next Data Display dynamic-state pass. This slice adds independent inline-expandable examples for Metric, ImageIcon, and RankedBarChart so host-driven metric refresh, detail/icon slot changes, provider resource switching, missing-path fallback, icon size handoff, ranked data refresh, compact density, max-visible changes, empty state, and accent refresh are represented by standalone AXAML samples below the owning component.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/MetricInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ImageIconInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/RankedBarChartInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- `CodexStatCard` and `CodexMetric` own value/detail/icon slot presence, so Docs should show host refreshes, detail toggles, icon swaps, and compact metric composition.
- `CodexImageIcon` reloads `Path` through Avalonia resource loading and clears `Source` on missing paths, so Docs should show provider switching, missing-resource fallback, size handoff, and disabled host composition.
- `CodexRankedBarChart` observes `ItemsSource`, compact state, row sizing, max-visible items, empty text, and accent brushes, so Docs should show collection refresh, compact density, empty state, and accent refresh.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `DataDisplay/MetricInteraction.axaml` with refresh update, detail toggle, icon swap, and compact metric examples. Status: completed.
- Add `DataDisplay/ImageIconInteraction.axaml` with resource switching, missing path fallback, size handoff, and disabled host composition examples. Status: completed.
- Add `DataDisplay/RankedBarChartInteraction.axaml` with data refresh, empty state, accent refresh, and compact comparison examples. Status: completed.
- Register all three examples under their owning component pages with matching preview builders. Status: completed.
- Extend static and rendered Docs guards so Metric, ImageIcon, and RankedBarChart join the multi-case inline-code render matrix across themes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~NavigationDataComponentTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A39: Layout Shell Sidebar Section Interaction Docs Cases

Status: completed for the next Layout interaction pass. This slice adds independent inline-expandable examples for Application shell, Sidebar primitives, and Section so navigation selection, sibling active clearing, badge refresh, footer actions, hover row actions, nested row selection, section action refresh, action slot toggles, empty body, and dense section composition are represented by standalone AXAML samples below the owning component.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/ApplicationShellInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/SidebarPrimitivesInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Layout/SectionInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- `CodexSidebarMenuButton` owns active, size, icon, and badge classes while sidebar actions and sub-buttons own their own active / hover-visible state, so Docs should show host navigation updates without replacing the shell.
- `CodexSection` owns title, description, actions, and body slot presence, so Docs should show action refresh, action slot removal/restoration, empty body, description-only header, and dense body composition.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `Layout/ApplicationShellInteraction.axaml` with sidebar active selection, sibling clearing, badge refresh, footer action, and stable section slot examples. Status: completed.
- Add `Layout/SidebarPrimitivesInteraction.axaml` with active menu rows, badge refresh, hover action, nested selection, and disabled guard examples. Status: completed.
- Add `Layout/SectionInteraction.axaml` with action refresh, action slot toggle, empty body, dense content, and description-only header examples. Status: completed.
- Register all three examples under their owning component pages with matching preview builders. Status: completed.
- Extend static and rendered Docs guards so Layout pages join the multi-case inline-code render matrix across themes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~NavigationDataComponentTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A40: Navigation Primitive And Motion Interaction Docs Cases

Status: completed for the current primitive/token interaction pass. This slice adds independent inline-expandable examples for Separator, Kbd, Typography, FocusRing, Overlay, and Motion so orientation toggles, shortcut replacement, role/wrap changes, focus-ring geometry, overlay dismiss/reopen policy, scrim toggles, runtime motion transitions, and reduced-motion handoff are represented by standalone AXAML samples below the owning component.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/SeparatorInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Navigation/KbdInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Primitives/TypographyInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Primitives/FocusRingInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Primitives/OverlayInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Tokens/MotionInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- `CodexSeparator` owns orientation and size classes, so Docs should show orientation switching, density cycling, toolbar separators, section dividers, and disabled host composition.
- `CodexKbd` owns size classes and is commonly composed into command and button shortcuts, so Docs should show host-driven shortcut replacement, chord spacing, command hints, and disabled hosts.
- `CodexText`, `CodexFocusRing`, `CodexOverlay`, and `CodexMotion` own the primitive contracts that Web-parity components reuse for role styles, focus-visible chrome, dismiss policy, scrim state, and runtime transition tokens.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `Navigation/SeparatorInteraction.axaml` and `Navigation/KbdInteraction.axaml` with matching preview builders. Status: completed.
- Add `Primitives/TypographyInteraction.axaml`, `Primitives/FocusRingInteraction.axaml`, and `Primitives/OverlayInteraction.axaml` with matching preview builders. Status: completed.
- Add `Tokens/MotionInteraction.axaml` with a runtime motion preview builder using `CodexMotion` duration/easing helpers. Status: completed.
- Extend static and rendered Docs guards so these remaining primitive/token pages join the multi-case inline-code render matrix across themes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ControlStateTests|FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~MotionRenderedLifecycleTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A41: Sheet Overlay Component And Docs Cases

Status: completed for this missing-core overlay pass. This slice adds `CodexSheet` as an edge-mounted drawer component so the library covers the Web/shadcn Sheet pattern instead of only centered dialogs and anchored popovers. It reuses the dialog dismissal, close command, Escape/outside-pointer policy, slot presence, and focus-return contract while adding side placement classes and edge-specific slide motion.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexSheet.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Sheet.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/Sheet.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/SheetStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/SheetAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/SheetInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- shadcn Sheet is Dialog-derived and uses side placement values `top`, `right`, `bottom`, and `left`; current CodexSwitchUI had dialog, command dialog, popover, tooltip, hover card, and overlay primitive coverage, but no edge-mounted drawer component.
- Existing `CodexDialog` already owns close command, dismiss command, Escape and outside-pointer policy, slot presence, open/closed classes, and focus restoration, so `CodexSheet` should extend that contract instead of duplicating dismissal logic.
- The component theme must own its ControlTemplate, focus adorner suppression, open/closed selectors, side selectors, and tokenized opacity/transform transitions to remain aligned with the library's Web-parity architecture.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add `CodexSheet` with `Side` property and `side-right`, `side-left`, `side-top`, and `side-bottom` classes. Status: completed.
- Add `Sheet.axaml` theme with Codex-owned template, close button, action/footer slot, side-specific placement, edge slide exit transforms, and tokenized transitions. Status: completed.
- Register the Sheet style in `ComponentStyles.axaml` and extend structure tests so the component must keep its own class file, style file, style selector, template, focus adorner suppression, and motion selectors. Status: completed.
- Add Docs Sheet page plus default, states, anatomy, and interaction AXAML samples with inline expandable code. Status: completed.
- Extend overlay behavior tests and rendered lifecycle tests for side classes, Dismiss/Escape/outside-pointer behavior, focus restoration, and mounted transform motion. Status: completed.
- Update docs-site overlay pages in English and Chinese so the public UI-system docs mention Sheet coverage and side-placement review criteria. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~OverlayRenderedLifecycleTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `npm run lint` in `docs-site`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent A18: Docs Multi-Case Visual Fingerprint Expansion

Status: completed for all current multi-case Docs pages. This slice makes the comparable visual baseline follow the rendered multi-case gallery instead of a stale hand-picked subset, so new lifecycle and interaction pages must produce stable expanded-code screenshots across Light, Dark, and Custom themes.

Scope:

- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`

Evidence gathered:

- `MultiCasePages` covered 19 pages after the latest Docs work, including `feedback.sonner`, `overlay.command-dialog`, and `overlay.tooltip`.
- `VisualFingerprintPages` still covered only the older 10 high-risk pages, so several newly added interaction/lifecycle examples were rendered in smoke tests but not protected by the comparable visual baseline.

Implementation targets:

- Make `VisualFingerprintPages` reuse `MultiCasePages` so visual baseline coverage cannot drift from the expanded inline-code gallery. Status: completed.
- Add a static guard that keeps Docs visual fingerprints tied to the multi-case page list. Status: completed.
- Regenerate `DocsVisualFingerprints.json` so all 19 multi-case pages have Light, Dark, and Custom expanded-code fingerprints. Status: completed with 57 signatures.

Verification:

- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`

### Agent A17: Docs Overlay And Feedback Lifecycle Cases

Status: completed for the next dynamic Overlay/Feedback Docs pass. This slice keeps the examples focused on the visible Web parity behaviors that need runtime confidence: toast lifecycle, command-dialog item selection, loading suppression, tooltip side/arrow states, and hover-card timing states.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Feedback/SonnerInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/CommandDialogInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/TooltipInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/HoverCardInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`

Evidence gathered:

- Sonner, CommandDialog, Tooltip, and HoverCard already had default/state examples, but did not show enough lifecycle and event-trigger variants in the inline case gallery.
- Docs already provide the required per-case workflow through `BuildInlineExample`: a rendered component first, then a local `Show code`/`Hide code` button that expands the exact AXAML sample beneath that component.

Implementation targets:

- Add independent AXAML lifecycle/interaction examples for Sonner, CommandDialog, Tooltip, and HoverCard. Status: completed.
- Register every new example as an inline-expandable `DocsExampleCase` under its owning component page. Status: completed.
- Extend static and rendered Docs tests so these dynamic examples are checked across light, dark, and custom themes with inline source expansion. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsPanelLayoutTests`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`

### Agent C55: Drawer Overlay Component And Docs Cases

Status: completed for this missing-core overlay pass. This slice adds a `CodexDrawer` component that follows the shadcn/Vaul drawer surface more closely than `CodexSheet`: direction placement, handle affordance, drag threshold state, drag release dismissal, scrollable body, sticky footer actions, Escape/outside-pointer dismissal, and focus return all live in the component contract and Docs examples.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDrawer.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Drawer.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/Drawer.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DrawerStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DrawerAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Overlay/DrawerInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayFeedbackComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/OverlayRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/overlay.mdx`
- `docs-site/content/docs/zh/ui-system/overlay.mdx`

Evidence gathered:

- shadcn Drawer is Vaul-backed and exposes direction, handle, drag dismissal, scrollable content, footer actions, and the same dialog dismissal/focus lifecycle as other overlays.
- `CodexDialog` already owns the shared close command, dismiss command, Escape and outside-pointer policy, open/closed classes, slot state, and focus restoration, so `CodexDrawer` extends the dialog contract instead of duplicating overlay dismissal plumbing.
- Docs already render each example first and use `BuildInlineExample` to place the local `Show code`/`Hide code` toggle under that specific component case, so Drawer needed independent AXAML files plus page registration rather than a right-rail-only sample.

Implementation targets:

- Add `CodexDrawer` with `Direction`, `Size`, `IsHandleVisible`, `ShouldScaleBackground`, `CloseOnDragDismiss`, `DragDismissThreshold`, drag state, pointer handle capture, and `DragCompleted` event. Status: completed.
- Add `Drawer.axaml` with a Codex-owned template, handle, header/description, close content, scrollable body, footer slot, direction placement selectors, tokenized open/closed edge motion, handle/drag-ready states, and split template transitions that Avalonia can compile safely. Status: completed.
- Register Drawer in component styles, top-level component structure guards, control state tests, overlay behavior tests, and rendered lifecycle tests. Status: completed.
- Add Docs Drawer page plus default, states, anatomy, and interaction AXAML samples with inline expandable code under each case. Status: completed.
- Extend Docs page registry, multi-case rendering, visual fingerprints, state matrix, event matrix, and docs-site overlay review notes in English and Chinese. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~OverlayFeedbackComponentTests|FullyQualifiedName~DocsPanelLayoutTests|FullyQualifiedName~OverlayRenderedLifecycleTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C56: Combobox Form Component And Docs Cases

Status: completed for this missing-core Forms pass. This slice adds a `CodexCombobox` component for shadcn-style filterable predefined selection, then wires the Docs page so each rendered example has its own inline `Show code` / `Hide code` AXAML block directly beneath the case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexCombobox.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Combobox.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/ButtonGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/InputGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/Combobox.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ComboboxStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ComboboxAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/ComboboxInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn Combobox is a command/popover-backed predefined selection pattern: text filters choices, highlighted item and selected item are distinct states, Enter commits, Escape closes, and clear/loading/empty paths are visible component states.
- Base UI Combobox confirms the same contracts through `open`, `value`, `inputValue`, `autoHighlight`, pointer hover highlight, disabled/read-only, popup unmount/motion, and callback events.
- WAI-ARIA combobox/listbox behavior requires the input to own popup navigation, Arrow/Home/End to move active option, Enter to commit, and Escape to dismiss.
- Semi.Avalonia keeps AutoCompleteBox and ComboBox concepts separate, which supports adding a dedicated filterable `CodexCombobox` rather than overloading `CodexSelect`.

Implementation targets:

- Add `CodexCombobox` with `ItemsSource`, inline `Items` content, `SelectedItem`, `Text`, `PlaceholderText`, `DisplayMemberPath`, `IsOpen`, `AutoHighlight`, hover highlight, clear visibility, close/open policies, loading, intent, size, and popup height. Status: completed.
- Add selection, input, and open change events plus keyboard handling for Down, Up, Home, End, Enter, and Escape. Status: completed.
- Add `CodexComboboxItem` with selected and highlighted classes, pointer hover highlight, click selection, and selected check indicator. Status: completed.
- Add `Combobox.axaml` with input group, clear action, trigger, popup, loading, empty, list, highlighted item, selected item, size, intent, and tokenized open/closed opacity/scale motion. Status: completed.
- Register Combobox in the theme index, button-group/input-group composition styles, component structure tests, state/intent tests, form behavior tests, Docs page registry, state/event matrices, multi-case rendered Docs tests, and visual fingerprints. Status: completed.
- Add independent AXAML samples for default, states, anatomy, and interaction. The samples use inline items so the code shown beneath each case is self-contained. Status: completed.
- Update docs-site Forms pages in English and Chinese to call out filterable predefined selection, popup motion, highlighted/selected item, clear, loading/empty, Arrow, Enter, and Escape behavior. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`

### Agent C62: Chart Composition Component And Docs Cases

Status: completed for this Data Display Chart parity pass. This slice adds shadcn-style Chart composition primitives while keeping chart rendering decoupled from a single engine, so CodexSwitchUI can wrap usage pie, ranked bar, ECharts trend, or custom chart content with shared container, legend, tooltip, config color, and refresh-motion contracts.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexChart.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Chart.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/Chart.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ChartStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ChartAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/ChartInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- shadcn Chart is a composition helper around Recharts rather than a locked chart abstraction; it provides `ChartContainer`, `ChartTooltipContent`, `ChartLegendContent`, config labels/colors/icons, and guidance to keep the chart engine replaceable.
- Local CodexSwitchUI already has concrete chart renderers (`CodexUsagePieChart`, `CodexRankedBarChart`, and ECharts `CsUsageTrendChart`), so the missing piece was the shared container/legend/tooltip/config surface and Docs page rather than another hard-coded renderer.
- Desktop Docs already place each rendered case above its own `Show code` / `Hide code` AXAML block, so Chart needed independent default/state/anatomy/interaction samples and registry coverage.

Implementation targets:

- Add `CodexChart`, `CodexChartContainer`, `CodexChartLegend`, `CodexChartLegendItem`, `CodexChartTooltipContent`, `CodexChartTooltipItem`, and `CodexChartSeriesConfig`. Status: completed.
- Add tokenized Chart styles with header/content/legend/tooltip/footer slots, indicator dot/line/square geometry, compact/size states, open/closed tooltip state, refresh bar, and TransitionKey content motion. Status: completed.
- Add Docs `data.chart` page plus default, states, anatomy, and interaction AXAML samples, each rendered before its inline source toggle. Status: completed.
- Extend component structure/state guards, Docs static registration, rendered lifecycle coverage, visual fingerprints, and docs-site EN/ZH notes. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C61: Data Table Docs Pattern And Inline Cases

Status: completed for this Data Display Docs parity pass. This slice adds the shadcn Data Table pattern as a first-class desktop Docs page, while keeping the implementation as an explicit composition of existing CodexSwitchUI primitives rather than introducing a rigid data-grid abstraction.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/DataTable.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/DataTableStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/DataTableAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/DataTableInteraction.axaml`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- shadcn Data Table is documented as a pattern built from Table plus TanStack state for filtering, sorting, pagination, visibility, row selection, row actions, and reusable composition rather than a single fixed component.
- Local `CodexTable`, `CodexTextBox`, `CodexDropdownButton`, `CodexCheckBox`, `CodexBadge`, and `CodexPagination` already provide the primitive surface needed for the desktop analogue.
- Existing desktop Docs `BuildInlineExample` already renders each example before the local `Show code` / `Hide code` AXAML block, so this slice adds independent AXAML files and registers them in the case gallery.

Implementation targets:

- Add a `data.data-table` page with default, states, anatomy, and interaction cases. Status: completed.
- Cover filter input, column visibility dropdown, selected rows, row action menus, empty results, loading/refresh opacity, amount sorting, and pagination controls. Status: completed.
- Add Data Table state/event matrix entries and rendered lifecycle coverage. Status: completed.
- Update docs-site Data Display EN/ZH copy to mention the Data Table pattern and per-case inline AXAML expansion. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent|FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`

### Agent C60: Field Composition Primitives And Docs Cases

Status: completed for this Forms parity pass. This slice expands `CodexField` from a single wrapper into the current shadcn Field composition family, then adds an independent Docs example whose rendered case exposes its own local `Show code` / `Hide code` AXAML block.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexField.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/Field.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/FieldGroup.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- The current shadcn component list includes Field as a core component and the Field docs define FieldSet, FieldLegend, FieldGroup, FieldContent, FieldTitle, FieldDescription, FieldSeparator, and FieldError in addition to the single Field wrapper.
- shadcn Field supports vertical, horizontal, and responsive orientations, invalid state, grouped fieldsets, choice-card-like horizontal field rows, and one-or-many validation messages.
- Local Docs already render each example first and use `BuildInlineExample` to place the local code toggle under that exact case, so the Field composition pass needed a new independent AXAML sample plus page registration.

Implementation targets:

- Add `CodexFieldOrientation`, `CodexFieldLegendVariant`, and extend `CodexField` with `Orientation`, `IsInvalid`, orientation classes, invalid class, and the existing label/description/message/required/intent state contract. Status: completed.
- Add `CodexFieldGroup`, `CodexFieldSet`, `CodexFieldLegend`, `CodexFieldContent`, `CodexFieldTitle`, `CodexFieldDescription`, `CodexFieldSeparator`, and `CodexFieldError` with component-owned state classes. Status: completed.
- Extend `Field.axaml` with templates and tokenized transitions for the new Field composition primitives, horizontal/responsive layout hooks, fieldset legend/description slots, separator content, and error list/message rendering. Status: completed.
- Add `Forms/FieldGroup.axaml` plus `BuildFieldGroupPreview`, and register it under the Field Docs page so the rendered example has its own inline code expansion. Status: completed.
- Update state/event matrices and docs-site Forms pages in English and Chinese to call out FieldGroup, FieldSet, FieldContent, FieldSeparator, FieldError, responsive layout, invalid state, and error updates. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C59: Aspect Ratio Data Display Component And Docs Cases

Status: completed for this missing-core Data Display pass. This slice adds `CodexAspectRatio` for shadcn-style fixed-ratio media slots, then wires the Docs page so every rendered case has its own local `Show code` / `Hide code` AXAML block directly beneath the example.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexAspectRatio.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/AspectRatio.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/AspectRatio.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/AspectRatioStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/AspectRatioAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/DataDisplay/AspectRatioInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/NavigationDataComponentTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/Snapshots/DocsVisualFingerprints.json`
- `docs-site/content/docs/en/ui-system/data-display.mdx`
- `docs-site/content/docs/zh/ui-system/data-display.mdx`

Evidence gathered:

- shadcn Aspect Ratio is a ratio primitive used for fixed video/media containers; the core API is a ratio value and content slot.
- Local Docs already satisfy the inline-code requirement through `BuildInlineExample`, so Aspect Ratio needed independent AXAML examples and registration in `ExampleCasesFor`.
- The previous Avalonia transition detach crash is guarded by cached Docs pages and rendered lifecycle tests, so the new page was added to representative and multi-case render coverage.

Implementation targets:

- Add `CodexAspectRatio` with `Ratio`, `FitMode`, `Size`, `HasContent`, `RatioText`, normalized ratio coercion, `CalculateRatioSize`, ratio text formatting, and `RatioChanged`. Status: completed.
- Add ratio and fit classes for `aspect-ratio`, `has-content`, `empty`, `ratio-square`, `ratio-video`, `ratio-portrait`, `ratio-landscape`, `fit-width`, `fit-height`, and `fit-contain`. Status: completed.
- Add `AspectRatio.axaml` with root, viewport, content host, empty placeholder, clipped content, hover feedback, disabled state, size variants, and tokenized opacity/transform/brush transitions. Status: completed.
- Register the control in component style includes, component structure guards, state class tests, data/navigation tests, Docs page registry, state/event matrices, rendered lifecycle tests, and visual fingerprints. Status: completed.
- Add independent Docs AXAML samples for default, states, anatomy, and interaction, each rendered before its inline code toggle. Status: completed.
- Update docs-site Data Display pages in English and Chinese to mention fixed video/square/portrait media, fit modes, clipped content, hover motion, and RatioChanged events. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~NavigationDataComponentTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsPanelLayoutTests"`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore`
- `dotnet test /data/CodexSwitch/CodexSwitch.Tests/CodexSwitch.Tests.csproj -f net10.0 --no-restore -p:PublishAot=false`
- `npm run lint` in `docs-site`
- `npm run build` in `docs-site`
- `git diff --check`
- `git -C /data/CodexSwitch/CodexSwitchUI diff --check`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~DocsMultiCaseExamplesExpandInlineCodeAndRenderAcrossThemes|FullyQualifiedName~DocsRepresentativePagesRenderScreenshotsAcrossThemes|FullyQualifiedName~DocsNavigationAndDarkThemeSwitchDoNotDetachAnimatedPageContent"`
- `CODEXSWITCHUI_UPDATE_DOCS_VISUAL_SNAPSHOTS=1 dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`
- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter FullyQualifiedName~DocsVisualFingerprintsMatchBaselineAcrossThemes`

### Agent C58: Date Picker Form Component And Docs Cases

Status: completed for this missing-core Forms pass. This slice adds `CodexDatePicker` for shadcn-style Popover plus Calendar date selection, then wires the Docs page so each rendered example has its own local `Show code` / `Hide code` AXAML block directly beneath the case.

Scope:

- `CodexSwitchUI/src/CodexSwitchUI/Controls/CodexDatePicker.cs`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/Controls/DatePicker.axaml`
- `CodexSwitchUI/src/CodexSwitchUI/Themes/ComponentStyles.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/DatePicker.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/DatePickerStates.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/DatePickerAnatomy.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/Examples/Axaml/Forms/DatePickerInteraction.axaml`
- `CodexSwitchUI/src/CodexSwitchUI.Docs/MainWindow.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ComponentStructureTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/ControlStateTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/FormComponentDetailTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsPanelLayoutTests.cs`
- `CodexSwitchUI/tests/CodexSwitchUI.Tests/DocsRenderedLifecycleTests.cs`
- `docs-site/content/docs/en/ui-system/forms.mdx`
- `docs-site/content/docs/zh/ui-system/forms.mdx`

Evidence gathered:

- shadcn Date Picker is still documented as a Popover plus Calendar composition, with basic, range, input, time, natural-language, and RTL examples.
- Local `CodexCalendar` and `CodexPopover` patterns already establish the calendar grid, disabled bounds, range selection, and popover entrance motion contracts, so Date Picker is built as an owned Avalonia control that composes Calendar internally rather than copying page-local markup.
- Docs already render each example first and use `BuildInlineExample` to place the local `Show code` / `Hide code` toggle under that exact component case, so Date Picker needed independent AXAML files plus page registration.

Implementation targets:

- Add `CodexDatePicker` with `SelectedDate`, `RangeStart`, `RangeEnd`, `DisplayDate`, min/max bounds, selection mode, week settings, placeholder, formatting, clear visibility, loading, open/close policies, intent, size, display text, and state classes. Status: completed.
- Add events for selected date, range, and open state changes plus trigger click, Enter/Space/Down open, Escape close, Backspace/Delete clear, pointer day sync, disabled bounds, and loading guards. Status: completed.
- Add `DatePicker.axaml` with input group, focus ring, calendar icon, selected/placeholder text, clear button, chevron rotation, popover surface, Calendar content, loading overlay, tokenized opacity/scale transitions, and state selectors for selected/range/open/disabled/loading. Status: completed.
- Register Date Picker in the theme index, top-level component structure guards, control state tests, form behavior tests, Docs page registry, state/event matrices, multi-case rendered Docs tests, and docs-site Forms pages. Status: completed.
- Add independent AXAML samples for default, states, anatomy, and interaction, each rendered before its inline code toggle. Status: completed.

Verification:

- `dotnet test tests/CodexSwitchUI.Tests/CodexSwitchUI.Tests.csproj -f net10.0 --no-restore --filter "FullyQualifiedName~ComponentStructureTests|FullyQualifiedName~ControlStateTests|FullyQualifiedName~FormComponentDetailTests|FullyQualifiedName~DocsPanelLayoutTests"`

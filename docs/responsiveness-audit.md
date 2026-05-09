# Responsiveness Audit

This document catalogues fixed-size layout issues found in the WPF XAML views and recommends fix strategies for each.

---

## MainWindow.xaml

### Issue 1 — Hardcoded minimum window size

- **Location:** `<Window MinWidth="1280" MinHeight="720" />`
- **Problem:** Prevents the window from being resized below 1280×720. The app becomes unusable on smaller or lower-resolution screens (e.g. Surface Go, portrait monitors, or remote desktop sessions at 1024×768).
- **Fix:** Lower the minimums to `MinWidth="400"` and `MinHeight="480"` so the window can shrink to phone-width. Implement a SizeChanged handler that switches between sidebar and drawer navigation based on breakpoint.

### Issue 2 — Fixed sidebar column width

- **Location:** `<ColumnDefinition Width="320" />`
- **Problem:** The 320-pixel sidebar always occupies space regardless of window width. Below approximately 1024px, the sidebar crowds the content area.
- **Fix:** Collapse the column to `Width="0"` when the window width drops below 1024px (Expanded breakpoint). Use a hamburger drawer overlay for Compact/Medium states instead.

### Issue 3 — Help panel fixed width and margin

- **Location:** `<Border x:Name="HelpPanel" Width="800" Margin="100,50" />`
- **Problem:** A 800px panel with 100px side margins requires at least 1000px of available window width before it overflows or clips.
- **Fix:** Replace `Width="800"` with `MaxWidth="800"` and remove the fixed side margin. Set `HorizontalAlignment="Right"` and use `Margin="0,50,0,0"` so the panel slides in from the right and scales down on narrower windows.

### Issue 4 — Toast panel with fixed offset

- **Location:** `<Border x:Name="ClaimToastPanel" MaxWidth="380" Margin="0,0,32,32" />`
- **Problem:** The 32px right margin works fine at full screen but can clip when the window is very narrow. MaxWidth is good but the anchor margin is static.
- **Fix:** Reduce `MaxWidth` to 340 and keep the margin at `0,0,32,32`. The panel is already using `Grid.ColumnSpan="2"` and `HorizontalAlignment="Right"`, so this is acceptable; ensure it is tested at 400px window width.

### Issue 5 — No responsive column collapse for the main layout

- **Location:** Two-column `Grid` with `ColumnDefinition Width="320"` and `ColumnDefinition Width="*"`.
- **Problem:** No star sizing and no mechanism to collapse the sidebar column at narrower widths.
- **Fix:** Use a `SizeChanged` event handler that calls `ApplyResponsiveLayout(width)`. When `width < 1024`, set `SidebarColumn.Width = new GridLength(0)` and hide `SidebarPanel`; show a compact top bar with hamburger drawer navigation instead.

---

## DashboardView.xaml

### Issue 1 — Three fixed-width columns in main layout

- **Location:**
  ```xml
  <ColumnDefinition Width="280" />
  <ColumnDefinition Width="8" />
  <ColumnDefinition Width="*" />
  <ColumnDefinition Width="8" />
  <ColumnDefinition Width="220" />
  ```
- **Problem:** Left sidebar (280px) and right sidebar (220px) are hardcoded. At 1024px window minus the 320px app sidebar, the content area is only 704px, which is tight for three fixed-width columns.
- **Fix:** Add `MinWidth` and `MaxWidth` constraints so columns shrink gracefully:
  ```xml
  <ColumnDefinition Width="280" MinWidth="180" MaxWidth="320" />
  <ColumnDefinition Width="8" />
  <ColumnDefinition Width="*" MinWidth="200" />
  <ColumnDefinition Width="8" />
  <ColumnDefinition Width="220" MinWidth="160" MaxWidth="260" />
  ```

### Issue 2 — Fixed center-panel channel list column

- **Location:** Sub-grid `<ColumnDefinition Width="180" />` inside the center panel.
- **Problem:** The 180px fixed channel list column does not adapt to available space.
- **Fix:** Change to `<ColumnDefinition Width="160" MinWidth="120" MaxWidth="200" />`.

### Issue 3 — No wrapping or stacking on narrow widths

- **Problem:** All three columns are always visible regardless of available width. On a 1024px window with the compact nav bar visible, content will be extremely crowded.
- **Fix:** For a full responsive solution, consider hiding the left or right sidebar below 1280px (app total), using a visibility trigger or a `Compact/Medium` flag from `IResponsiveLayoutService`.

### Issue 4 — No vertical scroll wrapper on the outer layout

- **Problem:** If content grows taller than the window, it clips at the bottom with no scrollbar.
- **Fix:** Wrap each sidebar column's content in a `ScrollViewer` (already done on the left sidebar). Ensure the right sidebar also has a `ScrollViewer`.

---

## RequestsView.xaml

### Issue 1 — Ratio-split columns with no minimum width

- **Location:**
  ```xml
  <ColumnDefinition Width="1.2*" />
  <ColumnDefinition Width="16" />
  <ColumnDefinition Width="*" />
  ```
- **Problem:** Star sizing proportionally shrinks both panes, so the detail pane can become unusably narrow below ~800px.
- **Fix:** Add `MinWidth` to both columns:
  ```xml
  <ColumnDefinition Width="1.2*" MinWidth="280" />
  <ColumnDefinition Width="16" />
  <ColumnDefinition Width="*" MinWidth="220" />
  ```

### Issue 2 — Filter bar horizontal StackPanel overflow

- **Location:** `<StackPanel Orientation="Horizontal" HorizontalAlignment="Center">`
- **Problem:** All filter controls are laid out in a single horizontal row. If the container is narrow, controls overflow or clip horizontally.
- **Fix:** Replace `StackPanel Orientation="Horizontal"` with a `WrapPanel`. Add `Margin="0,4,0,4"` to each child so wrapping looks clean.

### Issue 3 — Fixed-width empty-state TextBlock

- **Location:** `<TextBlock ... Width="300" />` inside the empty-state stack panel.
- **Problem:** A fixed 300px width will overflow if the detail column is narrower than 300px.
- **Fix:** Remove the fixed `Width="300"` and rely on `TextWrapping="Wrap"` with `HorizontalAlignment="Center"` to constrain the text naturally.

### Issue 4 — Fixed-width action buttons

- **Location:** `<Style x:Key="RequestActionButtonStyle"> <Setter Property="Width" Value="180" />`
- **Problem:** Fixed 180px button width does not adapt to narrower columns.
- **Fix:** Replace `Width="180"` with `MaxWidth="220"` so buttons are at most 220px but can shrink in narrow contexts.

---

## BlueprintExplorerView.xaml

### Issue 1 — Fixed left search pane width

- **Location:** `<ColumnDefinition Width="300" />`
- **Problem:** The left search/filter pane is always exactly 300px, consuming significant space on narrow windows.
- **Fix:** Change to `<ColumnDefinition Width="280" MinWidth="220" MaxWidth="340" />` so it can shrink slightly without losing usability.

### Issue 2 — Hardcoded workspace sub-grid column widths

- **Location:**
  ```xml
  <ColumnDefinition Width="540" />
  <ColumnDefinition Width="32" />
  <ColumnDefinition Width="540" />
  ```
- **Problem:** The two 540px fixed columns require at least 1112px of workspace width. With the 300px left pane and 320px sidebar, this requires a total window width of ~1800px to avoid overflow.
- **Fix:** Use star sizing with minimum widths:
  ```xml
  <ColumnDefinition Width="*" MinWidth="280" />
  <ColumnDefinition Width="32" />
  <ColumnDefinition Width="*" MinWidth="280" />
  ```

### Issue 3 — Component card and procurement panel fixed widths

- **Location:**
  - `<Border ... Width="540" HorizontalAlignment="Left" />` (component cards)
  - `<Border ... Width="540" HorizontalAlignment="Left" />` (procurement/modifiers panels)
- **Problem:** Fixed 540px widths prevent the cards from filling or shrinking with available column space.
- **Fix:** Remove `Width="540"` and set `HorizontalAlignment="Stretch"`. Optionally add `MaxWidth="600"` if visual width must be capped.

### Issue 4 — Blueprint header grid center column fixed at 200px

- **Location:** `<ColumnDefinition Width="200" />` (ORG OWNERS panel column in the blueprint header)
- **Problem:** 200px is fixed and does not adapt to viewport.
- **Fix:** Change to `<ColumnDefinition Width="Auto" MinWidth="140" MaxWidth="220" />` so the column sizes to its content within a bounded range.

---

## General Issues (All Views)

### Issue 1 — No outer vertical ScrollViewer

- **Problem:** Most views do not wrap their outermost layout in a `ScrollViewer`. If the window height is shorter than the minimum content height, content is clipped vertically with no scrolling.
- **Fix:** Wrap the root layout of each view's content area in a `<ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">`.

### Issue 2 — Fixed Grid column widths assume 1280px+ available space

- **Problem:** The combined sum of all fixed pixel column widths across the three-pane dashboard layout (sidebar 320 + left pane 280 + gutter 8 + right pane 220 + gutter 8 = 836px) means the center column gets only 444px at 1280px total width — and even less on narrower screens.
- **Fix:** Replace fixed column widths with `MinWidth`/`MaxWidth`-bounded star sizing throughout.

### Issue 3 — No responsive column collapsing for the main app sidebar

- **Problem:** The MainWindow sidebar is always visible (320px) with no mechanism to collapse it on narrow windows.
- **Fix:** Implemented via `ApplyResponsiveLayout()` in `MainWindow.xaml.cs` — the sidebar collapses and a compact top bar with drawer navigation appears when `width < 1024`.

### Issue 4 — Content will clip or overflow horizontally below 1024px

- **Problem:** None of the views are designed to function below 1024px. With the sidebar present, the effective content area can be as narrow as 300-400px.
- **Fix:** The responsive MainWindow collapses the sidebar on narrow widths, providing more content space. Individual views need `MinWidth` guards and wrapping to function at 600px+ effective content width.

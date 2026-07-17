# Changelog

## [0.1.0] - 2026-07-14

Initial version.

- Immediate-mode API (`IM.*`) callable from any `Update()`, reconciled into a retained
  UI Toolkit tree once per frame via a player-loop hook.
- Widgets: Label, Header, Button, Toggle, Foldout, Slider (float/int), TextField, FloatField,
  IntField, Dropdown, EnumPopup, ProgressBar, Image, Space, FlexibleSpace, Separator.
- Layout: Window (draggable, per-title identity), Row, Column, Box, Scroll, Indent.
- Auto-created host (UIDocument + PanelSettings + theme) on first call; no scene setup.
- Edit-mode reconciler tests; Basic Overlay sample.

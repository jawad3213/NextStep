---
trigger: always_on
---

# Antigravity Design System Enforcement Rules
**Source of Truth:** `DESIGN_SYSTEM.MD`

You are the Antigravity UI/UX Implementation Agent. Your primary mandate is to ensure every component, style, and layout you generate adheres EXCLUSIVELY to the specifications in the `DESIGN_SYSTEM.MD` file.

## 1. Strict Token Adherence
- **Colors:** DO NOT use any hex codes, RGB, or HSL values not explicitly defined in Section 2. Always prioritize the Semantic CSS Variables (e.g., `--color-primary`, `--color-bg-page`).
- **Typography:** Only use 'Lato'. Apply the semantic text styles from Section 3.6 (e.g., `heading-1`, `body-default`). Never "guess" a font size; use the predefined scale (11px, 12px, 14px, etc.).
- **Spacing:** Use the 8px base grid. All margins and paddings must be multiples of 4px or 8px using the `--space-` tokens defined in Section 4.1.

## 2. Component Logic
- **Buttons:**
    - Primary: `#1A91F0`, 700 weight, 4px radius.
    - Secondary: Transparent with 2px solid border.
    - Hover states: Must include `box-shadow: 0 4px 12px rgba(26, 145, 240, 0.3)`.
- **Inputs:** Default height is exactly `44px`. Focus state MUST use `--color-border-focus` with a `3px` alpha-blue shadow.
- **Cards:** Use `8px` border radius and `var(--shadow-sm)` by default.

## 3. Layout Constraints
- **Builder UI:** If generating the builder interface, you MUST follow the two-panel split:
    - Navbar: `60px` height.
    - Sidebar: `380px` width.
    - Preview: Centered A4/Letter card on `#F5F5F5` background.
- **Breakpoints:** Use the specific Zety breakpoints (576px, 768px, 992px, 1200px).

## 4. Prohibitions
- **NO Inline Styles:** Use the CSS classes defined in the component library (Section 7).
- **NO Deviant Icons:** Only use 2px stroke, rounded outline icons (Feather/Heroicons style).
- **NO Custom Transitions:** Only use the cubic-bezier curves defined in Section 8.1.

## 5. Verification Step
Before providing any code or design:
1. Cross-reference your hex codes against Section 2.
2. Cross-reference your spacing against Section 4.
3. If a style is not in `DESIGN_SYSTEM.MD`, do not invent it. Ask for clarification or use the closest "Neutral" token.
# Zety.com — Complete Design System Documentation
> Reverse-engineered from builder.zety.com, zety.com, and platform UI analysis  
> Last updated: April 2026

---

## Table of Contents

1. [Brand Identity & Philosophy](#1-brand-identity--philosophy)
2. [Color System](#2-color-system)
3. [Typography](#3-typography)
4. [Spacing & Layout](#4-spacing--layout)
5. [Grid & Breakpoints](#5-grid--breakpoints)
6. [Iconography](#6-iconography)
7. [Component Library](#7-component-library)
8. [Motion & Transitions](#8-motion--transitions)
9. [Shadows & Elevation](#9-shadows--elevation)
10. [Border & Radius System](#10-border--radius-system)
11. [Builder UI — Specific Patterns](#11-builder-ui--specific-patterns)
12. [Template Design Tokens](#12-template-design-tokens)
13. [Accessibility](#13-accessibility)

---

## 1. Brand Identity & Philosophy

### Brand Essence
Zety is a **professional-yet-approachable** SaaS resume builder. Its design language bridges corporate credibility with consumer warmth — it must feel trusted enough for serious job seekers while remaining frictionless for first-time users.

### Core Design Principles
| Principle | Description |
|-----------|-------------|
| **Clarity First** | Every UI decision reduces cognitive load. Minimal visual noise. |
| **Guided Confidence** | The wizard-step flow communicates progress, not complexity. |
| **Professional Polish** | Clean white surfaces with subtle blue-led brand presence. |
| **Accessible Utility** | High contrast ratios, clear labels, no ambiguity in CTAs. |
| **Responsive Trust** | Consistent rendering across device sizes reinforces reliability. |

---

## 2. Color System

### 2.1 Brand Primary — Blue

Zety's primary brand color is a confident, professional **medium-to-strong blue**. It is used for primary CTAs, active states, links, progress indicators, and key UI highlights.

```
Primary Blue:       #1A91F0   (main brand, buttons, links)
Primary Blue Dark:  #0F6EBB   (hover states on buttons)
Primary Blue Light: #E8F4FD   (tinted backgrounds, selected states, chips)
Primary Blue Pale:  #F0F8FF   (subtle section backgrounds)
#C8901A (Gold) - secondary color FOR MAIN BUTTONS
```

### 2.2 Brand Secondary — Teal/Green Accent

Used for success states, score indicators, resume checker badges, and positive feedback.

```
Success Green:      #34A853   (score pass, positive feedback)
Success Green Light:#E6F4EA   (success background tint)
Warning Amber:      #F59B00   (resume score medium, warnings)
Warning Light:      #FFF8E6   (warning background tint)
Error Red:          #D93025   (validation errors, destructive)
Error Light:        #FCE8E6   (error background tint)
```

### 2.3 Neutrals

```
White:              #FFFFFF   (page background, card surfaces)
Gray 50:            #FAFAFA   (subtle section background)
Gray 100:           #F5F5F5   (input backgrounds, skeleton loaders)
Gray 200:           #EEEEEE   (dividers, borders)
Gray 300:           #E0E0E0   (inactive borders)
Gray 400:           #BDBDBD   (placeholder text)
Gray 500:           #9E9E9E   (secondary/tertiary text)
Gray 600:           #757575   (body secondary text)
Gray 700:           #616161   (body primary text light)
Gray 800:           #424242   (body primary text)
Gray 900:           #212121   (headings, titles)
```

### 2.4 Semantic Color Tokens (CSS Variables)

```css
:root {
  /* Brand */
  --color-primary:           #1A91F0;
  --color-primary-hover:     #0F6EBB;
  --color-primary-active:    #0A5A9C;
  --color-primary-tint:      #E8F4FD;
  --color-primary-pale:      #F0F8FF;

  /* Feedback */
  --color-success:           #34A853;
  --color-success-tint:      #E6F4EA;
  --color-warning:           #F59B00;
  --color-warning-tint:      #FFF8E6;
  --color-error:             #D93025;
  --color-error-tint:        #FCE8E6;
  --color-info:              #1A91F0;
  --color-info-tint:         #E8F4FD;

  /* Text */
  --color-text-primary:      #212121;
  --color-text-secondary:    #616161;
  --color-text-tertiary:     #9E9E9E;
  --color-text-disabled:     #BDBDBD;
  --color-text-inverse:      #FFFFFF;
  --color-text-link:         #1A91F0;
  --color-text-link-hover:   #0F6EBB;

  /* Backgrounds */
  --color-bg-page:           #FFFFFF;
  --color-bg-surface:        #FAFAFA;
  --color-bg-elevated:       #FFFFFF;
  --color-bg-input:          #F5F5F5;
  --color-bg-overlay:        rgba(0, 0, 0, 0.5);

  /* Borders */
  --color-border-default:    #E0E0E0;
  --color-border-strong:     #BDBDBD;
  --color-border-focus:      #1A91F0;
  --color-border-error:      #D93025;

  /* Navigation */
  --color-nav-bg:            #FFFFFF;
  --color-nav-border:        #EEEEEE;
  --color-nav-text:          #424242;
  --color-nav-text-active:   #1A91F0;
}
```

### 2.5 Template Color Palette (Resume Builder)

Zety offers user-selectable accent colors for resume templates. The swatches provided:

```
Navy Blue:    #1A3A5C   (classic/corporate)
Blue:         #1A91F0   (default / modern)
Teal:         #00897B   (creative/fresh)
Green:        #2E7D32   (professional/growth)
Purple:       #6A1B9A   (creative fields)
Wine Red:     #8B1A2B   (bold/executive)
Dark Gray:    #424242   (minimal/elegant)
Charcoal:     #263238   (engineering/tech)
Black:        #000000   (ultra-minimal)
Gold/Amber:   #C8901A   (premium/luxury)
```

---

## 3. Typography

### 3.1 Font Families

Zety uses **Lato** as its primary typeface across the web platform. Lato is a humanist sans-serif with professional neutrality and excellent screen legibility.

```css
--font-family-primary:   'Lato', 'Helvetica Neue', Arial, sans-serif;
--font-family-mono:      'Courier New', Courier, monospace;  /* used in code/ATS previews */
```

**Web font loading:**
```html
<link href="https://fonts.googleapis.com/css2?family=Lato:wght@300;400;700;900&display=swap" rel="stylesheet">
```

### 3.2 Type Scale

```css
--font-size-xs:    11px;   /* badges, micro-labels */
--font-size-sm:    12px;   /* captions, helper text */
--font-size-base:  14px;   /* body default, form labels */
--font-size-md:    16px;   /* body large, card body */
--font-size-lg:    18px;   /* section subheadings */
--font-size-xl:    20px;   /* panel headings */
--font-size-2xl:   24px;   /* page section headings */
--font-size-3xl:   28px;   /* hero sub-headings */
--font-size-4xl:   36px;   /* hero headings, H1 */
--font-size-5xl:   48px;   /* marketing hero displays */
```

### 3.3 Font Weights

```css
--font-weight-light:    300;
--font-weight-regular:  400;
--font-weight-bold:     700;
--font-weight-black:    900;
```

### 3.4 Line Heights

```css
--line-height-tight:    1.2;    /* headings */
--line-height-snug:     1.35;   /* subheadings */
--line-height-normal:   1.5;    /* body text default */
--line-height-relaxed:  1.65;   /* long-form reading */
--line-height-loose:    2.0;    /* spaced lists */
```

### 3.5 Letter Spacing

```css
--letter-spacing-tight:  -0.01em;   /* large headings */
--letter-spacing-normal:  0;         /* body */
--letter-spacing-wide:    0.02em;    /* small caps, labels */
--letter-spacing-wider:   0.05em;    /* tags, badges */
--letter-spacing-widest:  0.1em;     /* uppercase labels */
```

### 3.6 Text Styles (Semantic)

| Style Name | Size | Weight | Line Height | Usage |
|-----------|------|--------|-------------|-------|
| `heading-hero` | 48px | 900 | 1.1 | Marketing hero H1 |
| `heading-1` | 36px | 700 | 1.2 | Page-level H1 |
| `heading-2` | 28px | 700 | 1.25 | Section headings |
| `heading-3` | 24px | 700 | 1.3 | Card headings |
| `heading-4` | 20px | 700 | 1.35 | Panel headings |
| `heading-5` | 18px | 700 | 1.4 | Widget headings |
| `heading-6` | 16px | 700 | 1.4 | Label headings |
| `body-large` | 16px | 400 | 1.6 | Hero body copy |
| `body-default` | 14px | 400 | 1.5 | Standard body |
| `body-small` | 12px | 400 | 1.5 | Helper text, captions |
| `label` | 12px | 700 | 1.2 | Form labels, tags |
| `overline` | 11px | 700 | 1.2 | Section overlines (all-caps) |
| `button-lg` | 16px | 700 | 1 | Large button text |
| `button-md` | 14px | 700 | 1 | Medium button text |
| `button-sm` | 12px | 700 | 1 | Small button text |

---

## 4. Spacing & Layout

### 4.1 Base Unit
Zety uses an **8px base grid** (with 4px for micro-spacing).

```css
--space-0:   0px;
--space-1:   4px;    /* micro: icon padding, tight inline gaps */
--space-2:   8px;    /* xs: between label and input */
--space-3:   12px;   /* sm: tight card padding */
--space-4:   16px;   /* md: standard component padding */
--space-5:   20px;   /* standard inline spacing */
--space-6:   24px;   /* lg: card inner padding */
--space-7:   28px;   /* form section gap */
--space-8:   32px;   /* xl: section spacing */
--space-10:  40px;   /* 2xl: major section gap */
--space-12:  48px;   /* 3xl: hero padding */
--space-16:  64px;   /* 4xl: between page sections */
--space-20:  80px;   /* page-level vertical rhythm */
--space-24:  96px;   /* hero section padding */
```

### 4.2 Component Spacing

```css
/* Buttons */
--btn-padding-y-lg:     14px;
--btn-padding-x-lg:     32px;
--btn-padding-y-md:     10px;
--btn-padding-x-md:     24px;
--btn-padding-y-sm:     6px;
--btn-padding-x-sm:     16px;

/* Inputs */
--input-padding-y:      10px;
--input-padding-x:      14px;
--input-height-default: 44px;
--input-height-sm:      36px;

/* Cards */
--card-padding:         24px;
--card-padding-sm:      16px;

/* Sidebar (Builder) */
--sidebar-width:        380px;
--sidebar-padding:      24px;
```

---

## 5. Grid & Breakpoints

### 5.1 Breakpoints

```css
--breakpoint-xs:   0px;       /* mobile portrait */
--breakpoint-sm:   576px;     /* mobile landscape */
--breakpoint-md:   768px;     /* tablet */
--breakpoint-lg:   992px;     /* desktop */
--breakpoint-xl:   1200px;    /* wide desktop */
--breakpoint-xxl:  1440px;    /* ultra-wide */
```

### 5.2 Container Max Widths

```css
--container-sm:    540px;
--container-md:    720px;
--container-lg:    960px;
--container-xl:    1140px;
--container-xxl:   1320px;
```

### 5.3 Grid System

- **12-column grid** on desktop/tablet
- **4-column grid** on mobile
- **Gutter width:** 24px desktop, 16px mobile
- **Outer margin:** 40px desktop, 16px mobile

### 5.4 Builder Layout

The resume builder uses a **two-panel split layout**:

```
┌─────────────────────────────────────────────────────────────┐
│  NAVBAR  (height: 60px)                                     │
├───────────────────────┬─────────────────────────────────────┤
│  LEFT SIDEBAR         │   LIVE PREVIEW (resume canvas)      │
│  width: 380px         │   flex: 1 remaining width           │
│  overflow-y: scroll   │   background: #F5F5F5               │
│  background: #FFFFFF  │   padding: 24px                     │
│  border-right: 1px    │   centered A4/Letter preview card   │
│  solid #EEEEEE        │   with box-shadow                   │
└───────────────────────┴─────────────────────────────────────┘
```

---

## 6. Iconography

### 6.1 Icon Style

Zety uses **outline/line-style icons** with consistent 2px stroke weight. Icons are clean, geometric, and rounded at corners — consistent with the Lato typeface's humanist character.

| Property | Value |
|----------|-------|
| Style | Line / Outline |
| Stroke weight | 2px |
| Corner type | Rounded |
| Line cap | Round |

### 6.2 Icon Sizes

```css
--icon-xs:    12px;   /* inline in body text */
--icon-sm:    16px;   /* button icons, form field icons */
--icon-md:    20px;   /* default UI icon size */
--icon-lg:    24px;   /* navigation icons, feature icons */
--icon-xl:    32px;   /* card/section feature icons */
--icon-2xl:   48px;   /* empty states, illustrations */
```

### 6.3 Icon Library

Zety primarily uses a custom internal icon set consistent with **Feather Icons** or **Heroicons (outline)** aesthetic. Common icons used:

| Icon | Context |
|------|---------|
| `check-circle` | Completion states, ATS pass indicators |
| `alert-circle` | Warnings, resume improvement suggestions |
| `edit-2` / `pencil` | Inline edit triggers on resume sections |
| `plus` / `plus-circle` | Add section, add entry |
| `trash-2` | Delete section/entry |
| `drag-handle` / `grip` | Drag-to-reorder resume sections |
| `download` | Download resume CTA |
| `eye` | Preview toggle |
| `lock` | Locked/premium features |
| `star` / `sparkles` | AI/Smart suggestion indicators |
| `check` | Checkbox checked state |
| `chevron-down` | Dropdowns, accordions |
| `chevron-right` | Breadcrumbs, navigation arrows |
| `x` / `close` | Dismiss modals, remove tags |
| `info` | Tooltip triggers, help hints |
| `user` | Contact info, profile sections |
| `briefcase` | Work experience section |
| `graduation-cap` | Education section |
| `tool` / `wrench` | Skills section |
| `globe` | Languages, website fields |
| `award` | Certifications section |
| `heart` | Interests section |
| `file-text` | Resume/document reference |
| `mail` | Email field |
| `phone` | Phone field |
| `map-pin` | Location field |
| `linkedin` | LinkedIn import/link |

### 6.4 Icon Color Behavior

```css
/* Default: inherits text color */
icon { color: inherit; }

/* Contextual icon colors */
.icon--primary   { color: var(--color-primary); }
.icon--success   { color: var(--color-success); }
.icon--warning   { color: var(--color-warning); }
.icon--error     { color: var(--color-error); }
.icon--muted     { color: var(--color-text-tertiary); }
```

---

## 7. Component Library

### 7.1 Buttons

#### Primary Button (Main CTA)
```css
.btn-primary {
  background-color: var(--color-primary);       /* #1A91F0 */
  color: #FFFFFF;
  font-family: var(--font-family-primary);
  font-size: 16px;
  font-weight: 700;
  padding: 14px 32px;
  border-radius: 4px;
  border: none;
  cursor: pointer;
  transition: background-color 0.2s ease, box-shadow 0.2s ease;
  text-align: center;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  letter-spacing: 0;
  white-space: nowrap;
}

.btn-primary:hover {
  background-color: var(--color-primary-hover);  /* #0F6EBB */
  box-shadow: 0 4px 12px rgba(26, 145, 240, 0.3);
}

.btn-primary:active {
  background-color: var(--color-primary-active);  /* #0A5A9C */
  box-shadow: none;
}

.btn-primary:disabled {
  background-color: #BDBDBD;
  cursor: not-allowed;
  box-shadow: none;
}
```

#### Secondary / Ghost Button
```css
.btn-secondary {
  background-color: transparent;
  color: var(--color-primary);
  border: 2px solid var(--color-primary);
  font-size: 16px;
  font-weight: 700;
  padding: 12px 30px;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-secondary:hover {
  background-color: var(--color-primary-tint);
}
```

#### Text Button / Link Button
```css
.btn-text {
  background: none;
  border: none;
  color: var(--color-primary);
  font-weight: 700;
  font-size: 14px;
  cursor: pointer;
  padding: 4px 8px;
  text-decoration: underline;
  transition: color 0.15s ease;
}

.btn-text:hover {
  color: var(--color-primary-hover);
}
```

#### Button Sizes
```
Large:   height 52px, padding 14px 32px, font 16px
Default: height 44px, padding 10px 24px, font 14px
Small:   height 34px, padding 6px 16px,  font 12px
```

---

### 7.2 Input Fields

```css
.input {
  width: 100%;
  height: 44px;
  background-color: #FFFFFF;
  border: 1.5px solid var(--color-border-default);   /* #E0E0E0 */
  border-radius: 4px;
  padding: 10px 14px;
  font-family: var(--font-family-primary);
  font-size: 14px;
  font-weight: 400;
  color: var(--color-text-primary);
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
  outline: none;
}

.input::placeholder {
  color: var(--color-text-tertiary);    /* #9E9E9E */
}

.input:focus {
  border-color: var(--color-border-focus);   /* #1A91F0 */
  box-shadow: 0 0 0 3px rgba(26, 145, 240, 0.15);
}

.input--error {
  border-color: var(--color-border-error);   /* #D93025 */
  box-shadow: 0 0 0 3px rgba(217, 48, 37, 0.1);
}

.input--success {
  border-color: var(--color-success);
}
```

#### Textarea
Same styles as `.input` but:
```css
.textarea {
  height: auto;
  min-height: 100px;
  resize: vertical;
  line-height: 1.5;
}
```

---

### 7.3 Form Labels & Helper Text

```css
.form-label {
  display: block;
  font-size: 13px;
  font-weight: 700;
  color: var(--color-text-secondary);  /* #616161 */
  margin-bottom: 6px;
  letter-spacing: 0.01em;
}

.form-helper {
  font-size: 12px;
  color: var(--color-text-tertiary);
  margin-top: 4px;
}

.form-error-text {
  font-size: 12px;
  color: var(--color-error);
  margin-top: 4px;
  display: flex;
  align-items: center;
  gap: 4px;
}
```

---

### 7.4 Cards

```css
.card {
  background-color: #FFFFFF;
  border-radius: 8px;
  border: 1px solid var(--color-border-default);  /* #E0E0E0 */
  padding: 24px;
  transition: box-shadow 0.2s ease;
}

.card:hover {
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.08);
}

.card--elevated {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
  border: none;
}

.card--selected {
  border: 2px solid var(--color-primary);
  box-shadow: 0 0 0 4px var(--color-primary-tint);
}
```

---

### 7.5 Badges & Tags

```css
/* Status badges */
.badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 3px 8px;
  border-radius: 12px;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.03em;
  text-transform: uppercase;
}

.badge--primary  { background: var(--color-primary-tint); color: var(--color-primary); }
.badge--success  { background: var(--color-success-tint); color: var(--color-success); }
.badge--warning  { background: var(--color-warning-tint); color: #C67A00; }
.badge--error    { background: var(--color-error-tint);   color: var(--color-error); }
.badge--neutral  { background: #F5F5F5;                   color: #616161; }
```

---

### 7.6 Progress Indicators

Used heavily in the step-by-step builder wizard:

```css
/* Stepper */
.stepper {
  display: flex;
  align-items: center;
  gap: 0;
}

.step {
  display: flex;
  align-items: center;
  gap: 8px;
}

.step__circle {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 700;
}

/* States */
.step--completed .step__circle {
  background-color: var(--color-success);
  color: #FFFFFF;
}

.step--active .step__circle {
  background-color: var(--color-primary);
  color: #FFFFFF;
}

.step--inactive .step__circle {
  background-color: #EEEEEE;
  color: #9E9E9E;
}

.step__connector {
  flex: 1;
  height: 2px;
  background-color: #EEEEEE;
  min-width: 40px;
}

.step__connector--completed {
  background-color: var(--color-success);
}
```

---

### 7.7 Resume Score Ring

The circular score indicator shown in the Resume Checker:

```css
/* SVG-based ring with these color bands */
.score-ring {
  --ring-bg:        #E0E0E0;     /* track background */
  --ring-low:       #D93025;     /* 0–49 score */
  --ring-medium:    #F59B00;     /* 50–74 score */
  --ring-high:      #34A853;     /* 75–100 score */
  
  width: 80px;
  height: 80px;
}

.score-ring__value {
  font-size: 20px;
  font-weight: 900;
  fill: var(--color-text-primary);
}
```

---

### 7.8 Tooltip

```css
.tooltip {
  background-color: #424242;
  color: #FFFFFF;
  border-radius: 4px;
  padding: 6px 10px;
  font-size: 12px;
  font-weight: 400;
  line-height: 1.4;
  max-width: 220px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.2);
  z-index: 1000;
}
```

---

### 7.9 Modal / Dialog

```css
.modal-overlay {
  background-color: rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(2px);
}

.modal {
  background: #FFFFFF;
  border-radius: 8px;
  padding: 32px;
  max-width: 520px;
  width: calc(100% - 32px);
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.2);
}

.modal__header {
  font-size: 20px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-bottom: 16px;
}

.modal__body {
  font-size: 14px;
  color: var(--color-text-secondary);
  line-height: 1.6;
}

.modal__footer {
  margin-top: 24px;
  display: flex;
  gap: 12px;
  justify-content: flex-end;
}
```

---

### 7.10 Navigation Bar

```css
.navbar {
  height: 60px;
  background-color: #FFFFFF;
  border-bottom: 1px solid #EEEEEE;
  display: flex;
  align-items: center;
  padding: 0 24px;
  position: sticky;
  top: 0;
  z-index: 100;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.06);
}

/* Logo area */
.navbar__logo {
  font-size: 22px;
  font-weight: 900;
  color: var(--color-primary);    /* #1A91F0 */
  letter-spacing: -0.02em;
}

/* Nav links */
.navbar__link {
  font-size: 14px;
  font-weight: 700;
  color: var(--color-text-secondary);
  text-decoration: none;
  padding: 4px 0;
  border-bottom: 2px solid transparent;
  transition: color 0.2s, border-color 0.2s;
}

.navbar__link:hover,
.navbar__link--active {
  color: var(--color-primary);
  border-bottom-color: var(--color-primary);
}
```

---

### 7.11 Dropdown / Select

```css
.select {
  appearance: none;
  background-image: url("data:image/svg+xml,...");   /* custom chevron-down */
  background-repeat: no-repeat;
  background-position: right 12px center;
  background-size: 16px;
  padding-right: 40px;
  /* inherits all .input styles */
  cursor: pointer;
}
```

---

### 7.12 Skill Tag (Resume Builder)

```css
.skill-tag {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background-color: var(--color-primary-tint);   /* #E8F4FD */
  color: var(--color-primary);
  border: 1px solid rgba(26, 145, 240, 0.2);
  border-radius: 20px;
  padding: 4px 12px;
  font-size: 13px;
  font-weight: 700;
  cursor: default;
  transition: background-color 0.15s;
}

.skill-tag__remove {
  cursor: pointer;
  color: var(--color-primary);
  opacity: 0.7;
  line-height: 0;
}

.skill-tag__remove:hover {
  opacity: 1;
}
```

---

### 7.13 AI Suggestion Chip

Used when AI auto-fills content suggestions (bullet points, summaries):

```css
.ai-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: linear-gradient(135deg, #E8F4FD 0%, #F0EBFF 100%);
  border: 1px solid rgba(26, 145, 240, 0.25);
  border-radius: 6px;
  padding: 8px 12px;
  font-size: 13px;
  font-weight: 400;
  color: var(--color-text-primary);
  cursor: pointer;
  transition: all 0.2s ease;
}

.ai-chip::before {
  content: "✨";
  font-size: 12px;
}

.ai-chip:hover {
  background: linear-gradient(135deg, #D4EBFC 0%, #E5DCFF 100%);
  border-color: var(--color-primary);
}
```

---

## 8. Motion & Transitions

### 8.1 Timing Functions

```css
--ease-default:   cubic-bezier(0.4, 0, 0.2, 1);   /* standard material ease */
--ease-in:        cubic-bezier(0.4, 0, 1, 1);
--ease-out:       cubic-bezier(0, 0, 0.2, 1);
--ease-spring:    cubic-bezier(0.34, 1.56, 0.64, 1);  /* slight overshoot */
```

### 8.2 Duration Tokens

```css
--duration-instant:   50ms;    /* micro feedback (checkbox check) */
--duration-fast:      100ms;   /* hover color shifts */
--duration-default:   200ms;   /* most transitions */
--duration-moderate:  300ms;   /* panel slides, modals */
--duration-slow:      400ms;   /* page sections, wizards */
--duration-crawl:     600ms;   /* loading skeletons */
```

### 8.3 Key Animations

```css
/* Fade in */
@keyframes fadeIn {
  from { opacity: 0; }
  to   { opacity: 1; }
}

/* Slide up (panels entering from below) */
@keyframes slideUp {
  from { transform: translateY(12px); opacity: 0; }
  to   { transform: translateY(0);    opacity: 1; }
}

/* Pulse (score ring animation, loading) */
@keyframes pulse {
  0%, 100% { opacity: 1; }
  50%       { opacity: 0.5; }
}

/* Skeleton shimmer */
@keyframes shimmer {
  0%   { background-position: -200% 0; }
  100% { background-position:  200% 0; }
}
```

---

## 9. Shadows & Elevation

Zety uses a minimal, clean elevation system based on light drop shadows.

```css
--shadow-none:   none;
--shadow-xs:     0 1px 2px rgba(0, 0, 0, 0.06);                          /* subtle borders */
--shadow-sm:     0 1px 4px rgba(0, 0, 0, 0.08);                          /* cards at rest */
--shadow-md:     0 4px 12px rgba(0, 0, 0, 0.08);                         /* hover cards */
--shadow-lg:     0 8px 24px rgba(0, 0, 0, 0.10);                         /* dropdowns, popovers */
--shadow-xl:     0 16px 40px rgba(0, 0, 0, 0.12);                        /* modals */
--shadow-2xl:    0 24px 64px rgba(0, 0, 0, 0.16);                        /* drawers, sheets */

/* Colored shadow for primary button hover */
--shadow-primary: 0 4px 12px rgba(26, 145, 240, 0.30);

/* Resume preview card shadow */
--shadow-resume: 0 8px 40px rgba(0, 0, 0, 0.14);
```

---

## 10. Border & Radius System

### 10.1 Border Radius

```css
--radius-none:   0;
--radius-sm:     2px;    /* sharp elements, table cells */
--radius-md:     4px;    /* buttons, inputs, small cards */
--radius-lg:     8px;    /* cards, panels */
--radius-xl:     12px;   /* modals, large surfaces */
--radius-2xl:    16px;   /* feature cards on marketing pages */
--radius-full:   9999px; /* pill buttons, tags, avatars */
```

### 10.2 Border Widths

```css
--border-thin:    1px;
--border-default: 1.5px;
--border-thick:   2px;
--border-heavy:   3px;
```

---

## 11. Builder UI — Specific Patterns

### 11.1 Section Header (within sidebar form)

```
┌─────────────────────────────────────────┐
│  ☰  Work Experience              [+ Add] │
│  Gray-200 bottom border                  │
└─────────────────────────────────────────┘
Font: 14px, 700 weight, #212121
Add button: text link style, primary color
Drag handle icon: left, 16px, #BDBDBD
```

### 11.2 Collapsible Entry Card (sidebar)

```
┌─────────────────────────────────────────┐
│  ☰  Software Engineer @ Acme Corp   [v] │
│     Jan 2022 – Present                  │
│  ──────────────────────────────────────  │
│  [expanded content fields]              │
└─────────────────────────────────────────┘
Collapsed state: 1 line summary + toggle
Expanded state: full field group visible
Border-left: 3px solid var(--color-primary) when active
```

### 11.3 Formatting Toolbar (Builder top bar)

```
[ Template ] [ Color • ] [ Font ▼ ] [ Size ▼ ] [ Spacing ▼ ]

Height: 48px
Background: #FAFAFA
Bottom border: 1px solid #EEEEEE
Font: 13px, 400 weight
Active control: outlined with primary color
Color swatch: 18px circle, 1px border
```

### 11.4 Progress Wizard Steps

```
(1) Template → (2) Contact → (3) Experience → (4) Education 
  → (5) Skills → (6) Summary → (7) Finalize
```

- Active step: filled blue circle + bold label
- Completed step: green circle with checkmark
- Future step: gray circle + muted label
- Connector line: gray default, green when passed

### 11.5 Tip/Hint Boxes

```css
.hint-box {
  background-color: var(--color-primary-pale);   /* #F0F8FF */
  border-left: 3px solid var(--color-primary);
  border-radius: 0 4px 4px 0;
  padding: 12px 16px;
  font-size: 13px;
  line-height: 1.5;
  color: var(--color-text-secondary);
}

.hint-box__icon {
  color: var(--color-primary);
  margin-right: 8px;
}
```

---

## 12. Template Design Tokens

Resume templates each define their own internal tokens, applied to the PDF/print output:

```css
/* Template: "Cubic" (default modern) */
--tpl-accent:         #1A91F0;
--tpl-heading-font:   'Lato', sans-serif;
--tpl-body-font:      'Lato', sans-serif;
--tpl-heading-size:   22px;
--tpl-name-size:      28px;
--tpl-body-size:      10px;
--tpl-section-gap:    14px;
--tpl-line-color:     #EEEEEE;
--tpl-sidebar-bg:     #F7FBFF;   /* only if two-column */

/* Template: "Diamond" (elegant serif-style) */
--tpl-accent:         #8B1A2B;
--tpl-heading-font:   'EB Garamond', serif;
--tpl-body-font:      'Lato', sans-serif;
--tpl-name-size:      30px;
--tpl-diamond-bullet: ◆;

/* Template: "Cascade" (timeline, modern) */
--tpl-accent:         #34A853;
--tpl-timeline-dot:   8px circle filled with accent;
--tpl-timeline-line:  1px solid #E0E0E0;
```

---

## 13. Accessibility

### 13.1 Contrast Ratios

| Combination | Ratio | WCAG AA |
|-------------|-------|---------|
| White text on `#1A91F0` (primary button) | 4.6:1 | ✅ Pass |
| `#212121` text on `#FFFFFF` | 16.1:1 | ✅ Pass |
| `#616161` on `#FFFFFF` (secondary text) | 5.9:1 | ✅ Pass |
| `#9E9E9E` on `#FFFFFF` (placeholder) | 2.8:1 | ⚠️ Decorative only |
| White on `#34A853` (success button) | 4.7:1 | ✅ Pass |
| White on `#D93025` (error) | 5.1:1 | ✅ Pass |

### 13.2 Focus States

```css
/* Global focus ring */
*:focus-visible {
  outline: 3px solid var(--color-primary);
  outline-offset: 2px;
  border-radius: 2px;
}

/* Remove focus for mouse users, keep for keyboard */
*:focus:not(:focus-visible) {
  outline: none;
}
```

### 13.3 Motion Reduction

```css
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    transition-duration: 0.01ms !important;
  }
}
```

### 13.4 ARIA Patterns

- Step wizard uses `aria-current="step"` on active step
- Form fields use `aria-required`, `aria-invalid`, `aria-describedby`
- Modals use `role="dialog"`, `aria-modal="true"`, `aria-labelledby`
- Score ring uses `role="img"` with descriptive `aria-label`
- Collapsible sections use `aria-expanded` + `aria-controls`

---

## Appendix A — Z-Index Scale

```css
--z-base:        1;
--z-dropdown:    100;
--z-sticky:      200;
--z-overlay:     300;
--z-modal:       400;
--z-notification:500;
--z-tooltip:     600;
```

## Appendix B — Key UI Measurements Summary

| Element | Value |
|---------|-------|
| Navbar height | 60px |
| Builder sidebar width | 380px |
| Card border radius | 8px |
| Button border radius | 4px |
| Input height | 44px |
| Input border radius | 4px |
| Base spacing unit | 8px |
| Primary font | Lato |
| Primary brand color | #1A91F0 |
| Page background | #FFFFFF |
| Section background | #FAFAFA |

---

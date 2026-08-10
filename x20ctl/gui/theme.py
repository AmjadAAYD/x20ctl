"""Visual theme.

One dark palette, defined once. Colours are chosen for contrast against the
background rather than for decoration: accent marks interactive things, muted
marks labels, and the warning colour is reserved for a macro that loops, which
is the one setting that can surprise a user.
"""

from __future__ import annotations

BG = "#14161a"
SURFACE = "#1c1f26"
SURFACE_HI = "#232733"
BORDER = "#2c3140"
BORDER_HI = "#3a4152"

TEXT = "#e6e9ef"
TEXT_MUTED = "#8b93a5"
TEXT_FAINT = "#5c6478"

ACCENT = "#5b9dff"
ACCENT_DIM = "#2f5fa8"
SUCCESS = "#4ec9a5"
WARNING = "#e0b354"
DANGER = "#e06c75"

RADIUS = "8px"

STYLESHEET = f"""
QWidget {{
    background: {BG};
    color: {TEXT};
    font-family: "Segoe UI", "Inter", sans-serif;
    font-size: 13px;
}}

/* Labels must not paint the window background, or they show as dark
   rectangles when they sit on a card. */
QLabel {{ background: transparent; }}

QLabel#Title {{
    font-size: 20px;
    font-weight: 600;
    color: {TEXT};
}}
QLabel#Subtitle {{
    font-size: 12px;
    color: {TEXT_MUTED};
}}
QLabel#SectionTitle {{
    font-size: 11px;
    font-weight: 700;
    color: {TEXT_FAINT};
    letter-spacing: 1px;
}}
QLabel#Muted   {{ color: {TEXT_MUTED}; }}
QLabel#Faint   {{ color: {TEXT_FAINT}; }}
QLabel#Success {{ color: {SUCCESS}; }}
QLabel#Warning {{ color: {WARNING}; }}
QLabel#Danger  {{ color: {DANGER}; }}

QFrame#Card {{
    background: {SURFACE};
    border: 1px solid {BORDER};
    border-radius: {RADIUS};
}}
QFrame#CardActive {{
    background: {SURFACE_HI};
    border: 1px solid {ACCENT_DIM};
    border-radius: {RADIUS};
}}
QFrame#Sidebar {{
    background: {SURFACE};
    border-right: 1px solid {BORDER};
}}
QFrame#Divider {{
    background: {BORDER};
    max-height: 1px;
    border: none;
}}

QPushButton {{
    background: {SURFACE_HI};
    border: 1px solid {BORDER_HI};
    border-radius: 6px;
    padding: 7px 14px;
    color: {TEXT};
}}
QPushButton:hover  {{ background: {BORDER}; border-color: {ACCENT_DIM}; }}
QPushButton:pressed{{ background: {BORDER_HI}; }}
QPushButton:disabled {{ color: {TEXT_FAINT}; border-color: {BORDER}; background: {SURFACE}; }}

QPushButton#Primary {{
    background: {ACCENT};
    border: 1px solid {ACCENT};
    color: #0b1220;
    font-weight: 600;
}}
QPushButton#Primary:hover    {{ background: #6fabff; border-color: #6fabff; }}
QPushButton#Primary:disabled {{ background: {ACCENT_DIM}; border-color: {ACCENT_DIM}; color: {TEXT_FAINT}; }}

QPushButton#Ghost {{
    background: transparent;
    border: 1px solid {BORDER_HI};
}}
QPushButton#Ghost:hover {{ background: {SURFACE_HI}; }}

QPushButton#Danger {{ background: transparent; border: 1px solid {BORDER_HI}; color: {DANGER}; }}
QPushButton#Danger:hover {{ background: rgba(224,108,117,0.12); border-color: {DANGER}; }}

QLineEdit, QSpinBox, QComboBox {{
    background: {BG};
    border: 1px solid {BORDER_HI};
    border-radius: 6px;
    padding: 6px 9px;
    selection-background-color: {ACCENT_DIM};
}}
QLineEdit:focus, QSpinBox:focus, QComboBox:focus {{ border-color: {ACCENT}; }}
QLineEdit::placeholder {{ color: {TEXT_FAINT}; }}

QSpinBox::up-button, QSpinBox::down-button {{
    width: 14px;
    background: transparent;
    border: none;
}}
QSpinBox::up-arrow, QSpinBox::down-arrow {{ width: 7px; height: 7px; }}

QComboBox::drop-down {{ border: none; width: 18px; }}
QComboBox QAbstractItemView {{
    background: {SURFACE_HI};
    border: 1px solid {BORDER_HI};
    selection-background-color: {ACCENT_DIM};
    outline: none;
}}

QListWidget {{
    background: transparent;
    border: none;
    outline: none;
}}
QListWidget::item {{
    padding: 9px 10px;
    border-radius: 6px;
    margin: 1px 6px;
}}
QListWidget::item:hover {{ background: {SURFACE_HI}; }}
QListWidget::item:selected {{ background: {ACCENT_DIM}; color: {TEXT}; }}

QSlider::groove:horizontal {{
    height: 5px;
    background: {BORDER};
    border-radius: 2px;
}}
QSlider::sub-page:horizontal {{ background: {ACCENT}; border-radius: 2px; }}
QSlider::handle:horizontal {{
    background: {TEXT};
    width: 15px;
    height: 15px;
    margin: -5px 0;
    border-radius: 7px;
}}
QSlider::handle:horizontal:hover {{ background: #ffffff; }}

QScrollBar:vertical {{ background: transparent; width: 9px; margin: 0; }}
QScrollBar::handle:vertical {{ background: {BORDER_HI}; border-radius: 4px; min-height: 28px; }}
QScrollBar::handle:vertical:hover {{ background: {TEXT_FAINT}; }}
QScrollBar::add-line, QScrollBar::sub-line {{ height: 0; }}
QScrollBar::add-page, QScrollBar::sub-page {{ background: none; }}

QToolTip {{
    background: {SURFACE_HI};
    color: {TEXT};
    border: 1px solid {BORDER_HI};
    padding: 5px 8px;
    border-radius: 5px;
}}
"""

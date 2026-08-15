"""Colours and stylesheet.

Warm greys with an ember accent. EMBER is only used for things you can click,
so it stays meaningful. State colours are muted so they don't shout.
"""

from __future__ import annotations

# -- surfaces, warm over blue-black ---------------------------------
BG = "#131110"
SURFACE = "#1B1817"
SURFACE_HI = "#241F1D"
SURFACE_TOP = "#2C2624"
LINE = "#332C29"
LINE_HI = "#453B36"

# -- type ------------------------------------------------------------------
TEXT = "#F4F0EB"
TEXT_MUTED = "#A79C92"
TEXT_FAINT = "#6B625B"

# -- accent and states -----------------------------------------------------
EMBER = "#FF8A5B"          # the only interactive colour
EMBER_DEEP = "#C4613A"
EMBER_GLOW = "rgba(255, 138, 91, 0.14)"
GOLD = "#F2C14E"           # attention, not danger
SAGE = "#86C08A"           # success
ROSE = "#E5645E"           # destructive
INK = "#171310"            # text on top of the accent

RADIUS = "10px"
RADIUS_SM = "7px"

STYLESHEET = f"""
QWidget {{
    background: {BG};
    color: {TEXT};
    font-family: "Segoe UI Variable Text", "Segoe UI", "Inter", sans-serif;
    font-size: 13px;
}}

/* Labels must never paint the window colour, or they show as dark
   rectangles the moment they sit on a raised surface. */
QLabel {{ background: transparent; }}

QLabel#Display {{
    font-size: 34px;
    font-weight: 300;
    letter-spacing: -1px;
    color: {TEXT};
}}
QLabel#Title {{
    font-size: 19px;
    font-weight: 600;
    letter-spacing: -0.3px;
}}
QLabel#Wordmark {{
    font-size: 17px;
    font-weight: 700;
    letter-spacing: 0.5px;
    color: {TEXT};
}}
QLabel#Section {{
    font-size: 10px;
    font-weight: 700;
    color: {TEXT_FAINT};
    letter-spacing: 1.6px;
}}
QLabel#Muted   {{ color: {TEXT_MUTED}; }}
QLabel#Faint   {{ color: {TEXT_FAINT}; }}
QLabel#Success {{ color: {SAGE}; }}
QLabel#Warning {{ color: {GOLD}; }}
QLabel#Danger  {{ color: {ROSE}; }}
QLabel#Accent  {{ color: {EMBER}; }}

QFrame#Sidebar {{
    background: {SURFACE};
    border: none;
    border-right: 1px solid {LINE};
}}
QFrame#Rule {{
    background: {LINE};
    max-height: 1px;
    border: none;
}}
QFrame#Card {{
    background: {SURFACE};
    border: 1px solid {LINE};
    border-radius: {RADIUS};
}}

/* -- buttons ---------------------------------------------------------- */
QPushButton {{
    background: {SURFACE_HI};
    border: 1px solid {LINE_HI};
    border-radius: {RADIUS_SM};
    padding: 8px 15px;
    color: {TEXT};
    font-weight: 500;
}}
QPushButton:hover {{
    background: {SURFACE_TOP};
    border-color: {EMBER_DEEP};
    color: {TEXT};
}}
QPushButton:pressed {{ background: {LINE}; }}
QPushButton:disabled {{
    background: {SURFACE};
    border-color: {LINE};
    color: {TEXT_FAINT};
}}

QPushButton#Primary {{
    background: {EMBER};
    border: 1px solid {EMBER};
    color: {INK};
    font-weight: 650;
    padding: 9px 20px;
}}
QPushButton#Primary:hover {{ background: #FF9E75; border-color: #FF9E75; }}
QPushButton#Primary:pressed {{ background: {EMBER_DEEP}; border-color: {EMBER_DEEP}; }}
QPushButton#Primary:disabled {{
    background: {SURFACE_HI}; border-color: {LINE_HI}; color: {TEXT_FAINT};
}}

QPushButton#Ghost {{ background: transparent; border: 1px solid {LINE_HI}; }}
QPushButton#Ghost:hover {{ background: {EMBER_GLOW}; border-color: {EMBER_DEEP}; }}
QPushButton#Ghost:checked {{
    background: {EMBER_GLOW}; border-color: {EMBER}; color: {EMBER};
}}

QPushButton#Danger {{ background: transparent; border: 1px solid {LINE_HI}; color: {ROSE}; }}
QPushButton#Danger:hover {{ background: rgba(229,100,94,0.12); border-color: {ROSE}; }}

QPushButton#Recording {{
    background: {ROSE}; border: 1px solid {ROSE}; color: {INK}; font-weight: 650;
}}

/* -- inputs ----------------------------------------------------------- */
QLineEdit, QSpinBox {{
    background: {BG};
    border: 1px solid {LINE};
    border-radius: {RADIUS_SM};
    padding: 7px 10px;
    selection-background-color: {EMBER_DEEP};
    selection-color: {TEXT};
}}
QLineEdit:hover, QSpinBox:hover {{ border-color: {LINE_HI}; }}
QLineEdit:focus, QSpinBox:focus {{ border-color: {EMBER}; background: {SURFACE}; }}
QLineEdit:disabled, QSpinBox:disabled {{ color: {TEXT_FAINT}; background: {SURFACE}; }}

QSpinBox::up-button, QSpinBox::down-button {{ width: 0; border: none; }}

QToolButton#Info {{
    background: transparent;
    color: {TEXT_FAINT};
    border: 1px solid {LINE_HI};
    border-radius: 8px;
    font-size: 9px;
    font-weight: 700;
    font-style: italic;
}}
QToolButton#Info:hover {{
    background: {EMBER}; color: {INK}; border-color: {EMBER};
}}

/* -- lists ------------------------------------------------------------ */
QListWidget {{ background: transparent; border: none; outline: none; }}
QListWidget::item {{
    padding: 10px 12px;
    border-radius: {RADIUS_SM};
    margin: 2px 10px;
    color: {TEXT_MUTED};
}}
QListWidget::item:hover {{ background: {SURFACE_HI}; color: {TEXT}; }}
QListWidget::item:selected {{
    background: {EMBER_GLOW};
    color: {EMBER};
    border-left: 2px solid {EMBER};
}}

/* -- slider ----------------------------------------------------------- */
QSlider::groove:horizontal {{ height: 4px; background: {LINE}; border-radius: 2px; }}
QSlider::sub-page:horizontal {{
    background: qlineargradient(x1:0, y1:0, x2:1, y2:0,
                                stop:0 {EMBER_DEEP}, stop:1 {EMBER});
    border-radius: 2px;
}}
QSlider::handle:horizontal {{
    background: {TEXT};
    width: 14px; height: 14px;
    margin: -5px 0;
    border-radius: 7px;
}}
QSlider::handle:horizontal:hover {{ background: #FFFFFF; }}

/* -- scrollbar -------------------------------------------------------- */
QScrollBar:vertical {{ background: transparent; width: 10px; margin: 0; }}
QScrollBar::handle:vertical {{
    background: {LINE_HI}; border-radius: 5px; min-height: 30px;
}}
QScrollBar::handle:vertical:hover {{ background: {TEXT_FAINT}; }}
QScrollBar::add-line, QScrollBar::sub-line {{ height: 0; }}
QScrollBar::add-page, QScrollBar::sub-page {{ background: none; }}

QToolTip {{
    background: {SURFACE_TOP};
    color: {TEXT};
    border: 1px solid {LINE_HI};
    padding: 8px 10px;
    border-radius: {RADIUS_SM};
}}

QMessageBox, QInputDialog {{ background: {SURFACE}; }}

/* -- start screen ---------------------------------------------------- */

QLabel#PageTitle {{ font-size: 25px; font-weight: 600; color: {TEXT}; }}
QLabel#PageSubtitle {{ font-size: 13px; color: {TEXT_FAINT}; }}

QFrame#EmptyPanel {{
    background: {SURFACE};
    border: 1px dashed {LINE_HI};
    border-radius: {RADIUS};
}}
QLabel#EmptyHeadline {{ font-size: 17px; color: {TEXT_MUTED}; }}
QLabel#EmptyHint {{ font-size: 13px; color: {TEXT_FAINT}; }}

QPushButton#AddTile {{
    background: {SURFACE};
    border: 1px solid {LINE_HI};
    border-radius: {RADIUS};
    color: {EMBER};
    font-size: 24px;
    font-weight: 600;
}}
QPushButton#AddTile:hover {{
    background: {EMBER_GLOW};
    border-color: {EMBER};
}}
QPushButton#AddTile:disabled {{
    color: {TEXT_FAINT};
    border-color: {LINE};
    font-size: 13px;
    font-weight: 400;
}}

QFrame#ControllerRow {{
    background: {SURFACE};
    border: 1px solid {LINE};
    border-radius: {RADIUS};
}}
QFrame#ControllerRow:hover {{ border-color: {EMBER_DEEP}; background: {SURFACE_HI}; }}

QLabel#PlayerBadge {{
    background: {EMBER_GLOW};
    border: 1px solid {EMBER_DEEP};
    border-radius: {RADIUS_SM};
    color: {EMBER};
    font-size: 15px;
    font-weight: 600;
}}
QLabel#RowTitle {{ font-size: 15px; color: {TEXT}; }}
QLabel#RowDetail {{ font-size: 12px; color: {TEXT_FAINT}; }}
QLabel#RowState {{ font-size: 12px; color: {SAGE}; }}
QLabel#RowStateIdle {{ font-size: 12px; color: {TEXT_FAINT}; }}
QLabel#VersionLink {{ font-size: 12px; }}

/* -- workspace sidebar ------------------------------------------------ */

QFrame#NavRail {{
    background: {SURFACE};
    border: none;
    border-right: 1px solid {LINE};
}}
QLabel#RailHeading {{
    font-size: 11px;
    font-weight: 600;
    color: {TEXT_FAINT};
    padding: 2px 8px 10px 8px;
}}
QPushButton#RailItem {{
    background: transparent;
    border: none;
    border-radius: {RADIUS_SM};
    color: {TEXT_MUTED};
    font-size: 14px;
    text-align: left;
    padding: 9px 12px;
}}
QPushButton#RailItem:hover {{ background: {SURFACE_HI}; color: {TEXT}; }}
QPushButton#RailItem:checked {{
    background: {EMBER_GLOW};
    color: {EMBER};
    font-weight: 600;
}}

/* -- test tab --------------------------------------------------------- */

QLabel#Lamp {{
    background: {SURFACE};
    border: 1px solid {LINE};
    border-radius: {RADIUS_SM};
    color: {TEXT_FAINT};
    font-size: 13px;
}}
QLabel#Lamp[lit="yes"] {{
    background: {EMBER_GLOW};
    border-color: {EMBER};
    color: {EMBER};
    font-weight: 600;
}}

/* -- macro grid ------------------------------------------------------- */

QPushButton#Cell {{
    background: {SURFACE};
    border: 1px solid {LINE};
    border-radius: 5px;
}}
QPushButton#Cell:hover {{ border-color: {EMBER_DEEP}; }}
QPushButton#Cell:checked {{
    background: {EMBER};
    border-color: {EMBER};
}}
QPushButton#Cell:checked:hover {{ background: {EMBER_DEEP}; }}
"""

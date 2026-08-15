"""The roster: which controllers are added, and which player each one is.

No Qt here. The rules about player numbers are worth testing on their own,
because the start screen and every page after it depend on them.
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from x20ctl.gui.roster import (
    MAX_PLAYERS, AlreadyAdded, PlayerTaken, Roster, RosterFull,
)

A = "98:B6:ED:E3:15:C4"
B = "98:B6:ED:E3:15:C5"


def test_a_new_roster_is_empty_and_falsy():
    """The start screen shows its empty state on exactly this."""
    roster = Roster()
    assert not roster
    assert len(roster) == 0
    assert roster.free() == [1, 2, 3, 4]


def test_the_first_controller_becomes_player_one():
    roster = Roster()
    slot = roster.add(A, product="EasySMX X20")
    assert slot.player == 1
    assert roster


def test_players_fill_the_lowest_free_number():
    roster = Roster()
    roster.add(A)
    roster.add(B, player=4)
    third = roster.add("98:B6:ED:E3:15:C6")
    assert third.player == 2, "2 is free, so it goes there rather than 5"


def test_a_player_can_be_chosen_outright():
    roster = Roster()
    slot = roster.add(A, product="EasySMX X10", player=3)
    assert slot.label == "EasySMX X10, P3"


def test_two_controllers_cannot_share_a_player():
    roster = Roster()
    roster.add(A, player=2)
    try:
        roster.add(B, player=2)
    except PlayerTaken as exc:
        assert "P2" in str(exc)
        return
    raise AssertionError("P2 was already taken")


def test_the_same_controller_cannot_be_added_twice():
    """Identity is the address. Adding a pad you already added is a mistake,
    not a second controller."""
    roster = Roster()
    roster.add(A)
    try:
        roster.add(A.lower())
    except AlreadyAdded:
        return
    raise AssertionError("the same address must be rejected")


def test_a_fifth_controller_is_refused_because_there_is_no_p5():
    roster = Roster()
    for i in range(MAX_PLAYERS):
        roster.add(f"98:B6:ED:E3:15:C{i}")
    try:
        roster.add("98:B6:ED:E3:15:FF")
    except RosterFull as exc:
        assert "4" in str(exc)
        return
    raise AssertionError("four players is the ceiling")


def test_removing_frees_the_player_number():
    roster = Roster()
    roster.add(A, player=1)
    roster.remove(1)
    assert roster.free() == [1, 2, 3, 4]
    assert roster.add(B, player=1).player == 1


def test_a_controller_can_be_renumbered():
    roster = Roster()
    roster.add(A, player=3)
    slot = roster.move(3, 1)
    assert slot.player == 1
    assert roster.taken() == [1]


def test_renumbering_onto_a_taken_player_is_refused():
    roster = Roster()
    roster.add(A, player=1)
    roster.add(B, player=2)
    try:
        roster.move(2, 1)
    except PlayerTaken:
        return
    raise AssertionError("P1 is occupied")


def test_save_files_are_keyed_per_controller_not_per_name():
    """Two identical pads report the same name, so the address is the key."""
    roster = Roster()
    first = roster.add(A, product="EasySMX X20")
    second = roster.add(B, product="EasySMX X20")
    assert first.save_key != second.save_key
    assert ":" not in first.save_key


def test_controllers_come_back_in_player_order():
    roster = Roster()
    roster.add(A, player=4, product="X10")
    roster.add(B, player=2, product="X20")
    assert [s.player for s in roster.ordered()] == [2, 4]

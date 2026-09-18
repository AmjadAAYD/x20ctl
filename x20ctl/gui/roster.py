"""Which controllers the app knows about, and which player each one is.

The app opens on an empty roster rather than on somebody's macros. You add a
controller, it takes a player number, and everything after that is scoped to
the one you picked. Four is the ceiling because the players are P1 to P4.

Save files are per controller, not global: two pads on one desk should not
fight over the same profile list. The address is the identity, since a pad's
name is whatever the vendor shipped and two X20s share it.
"""

from __future__ import annotations

from dataclasses import dataclass, field

MAX_PLAYERS = 4
PLAYERS = tuple(range(1, MAX_PLAYERS + 1))


class RosterFull(Exception):
    """Four controllers already have player numbers."""


class PlayerTaken(Exception):
    """That player number belongs to another controller."""


class AlreadyAdded(Exception):
    """That controller is already in the roster."""


@dataclass
class Slot:
    """One controller, and the player it is standing in for."""

    player: int
    address: str
    name: str = "Controller"
    product: str | None = None
    connected: bool = False

    @property
    def label(self) -> str:
        """What the roster row reads, e.g. 'EasySMX X20, P2'."""
        return f"{self.product or self.name}, P{self.player}"

    @property
    def save_key(self) -> str:
        """A filesystem-safe key for this controller's own save files.

        Keyed on the address rather than the name: two identical pads have the
        same name and must not share profiles.
        """
        return self.address.replace(":", "").replace("-", "").lower()


@dataclass
class Roster:
    """Up to four controllers, at most one per player number."""

    slots: dict[int, Slot] = field(default_factory=dict)

    def __len__(self) -> int:
        return len(self.slots)

    def __bool__(self) -> bool:
        """An empty roster is falsy, which is what the start screen asks."""
        return bool(self.slots)

    def taken(self) -> list[int]:
        return sorted(self.slots)

    def free(self) -> list[int]:
        return [player for player in PLAYERS if player not in self.slots]

    def ordered(self) -> list[Slot]:
        """Every controller, in player order."""
        return [self.slots[player] for player in sorted(self.slots)]

    def by_address(self, address: str) -> Slot | None:
        for slot in self.slots.values():
            if slot.address.lower() == address.lower():
                return slot
        return None

    def add(self, address: str, *, name: str = "Controller",
            product: str | None = None, player: int | None = None) -> Slot:
        """Put a controller in the roster, on `player` or the lowest free one."""
        if self.by_address(address) is not None:
            raise AlreadyAdded(f"{address} is already added")
        if player is None:
            if not self.free():
                raise RosterFull(
                    f"all {MAX_PLAYERS} player slots are taken; remove one first")
            player = self.free()[0]
        elif player not in PLAYERS:
            raise ValueError(f"player must be 1-{MAX_PLAYERS}, got {player}")
        elif player in self.slots:
            raise PlayerTaken(
                f"P{player} is {self.slots[player].label}; pick another")

        slot = Slot(player=player, address=address, name=name, product=product)
        self.slots[player] = slot
        return slot

    def remove(self, player: int) -> Slot | None:
        return self.slots.pop(player, None)

    def move(self, player: int, to: int) -> Slot:
        """Renumber a controller, e.g. dragging P3 to P1."""
        if player not in self.slots:
            raise KeyError(f"no controller on P{player}")
        if to not in PLAYERS:
            raise ValueError(f"player must be 1-{MAX_PLAYERS}, got {to}")
        if to in self.slots and to != player:
            raise PlayerTaken(f"P{to} is already {self.slots[to].label}")
        slot = self.slots.pop(player)
        slot.player = to
        self.slots[to] = slot
        return slot

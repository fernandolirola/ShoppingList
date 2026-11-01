# Changelog

## 1.1.4

- Show every pending next upgrade in the overview, including areas with unmet prerequisites. Locked areas start unticked.
- Refresh the area list with the profile instead of keeping only the first list received.
- Unselected areas can be expanded and collapsed independently of their purchase checkboxes. Their requirements preview your stash and donations without reserving items or adding purchases.
- Require the recipe station to be built and its required level met before suggesting a craft, including recipes without an Area requirement.
- Hide automatic upgrade stages, including the internal Weapon Rack storage shown as hideout_area_25_name.
- Keep the overview and future upgrades available when every area is unticked, without duplicating locked next upgrades in the future section.

## 1.1.3

- Fixed small stutters in raid on big maps: the plugin no longer searches the whole scene for hideout screens while you are in a raid.

## 1.1.2

- Fixed the rouble and euro signs showing as garbled text (for example in Total cost), and the accent in a Portuguese message.

## 1.1.1

- Fixed lag in raid: the list asks the server once when opened, then counts what you carry on the game side while it is on screen.
- The list now opens with ] (Ctrl+L inspects the weapon in raid). The setting is now called "Open key", so it resets to ] once.

## 1.1.0

- Open the list in raid (Ctrl+L with the inventory open). It counts what you carry, marks it "on you" and updates as you loot. Buying stays in the hideout.
- Sort areas by Default, Progress % or Rarity; click again to flip the order.
- Missing items show their rarity (common, rare, very rare). Area headers show a progress %.
- "Show future upgrades" lists the levels after the next one, with what your stash already covers.
- Refresh button, and the list refreshes by itself after buying, donating or buying by hand.
- After buying, the list comes back with your progress.
- Fixed the list not opening when clicking the button quickly.

## 1.0.0

- Shopping list for hideout upgrades: buys missing items from traders, then the flea market.
- Found-in-raid items with an unlocked craft get that craft's ingredients bought instead.
- Review screen with item icons, purpose, source and price before buying.
- Tick or untick areas, crafts and single items; fold areas and crafts.
- Craft check shows how many runs your stash already covers.
- Cart button in the hideout toolbar, a button in each area window, and Ctrl+L.
- Reads HideoutInProgress donations, HideoutBackport areas and SVM found-in-raid purchase settings.
- English and Portuguese.

# Changelog

## 1.2.4

- With BetterFlea, items no longer show as "Not for sale" or "Partly for sale" when the flea has offers for them.

## 1.2.3

- The hideout toolbar button's blue highlight now follows whether the window is open, so closing the window with Esc or another control clears it too.

## 1.2.2

- The Shopping list button now sits with the other hideout buttons when Vertical Hideout is installed, instead of on top of the area list. Thanks to Recker for reporting it.

## 1.2.1

- The Shopping list button no longer shows on areas that cannot be upgraded any more, such as the Library and the Cultist Circle.

## 1.2.0

- The review screen shows each item's purchase live: queued, buying, bought, or why it failed (sold out, price went up, no money, no space, trader limit), with a progress bar per item.
- The confirm button turns into a progress bar while buying, with a Stop button and what was spent so far.
- After buying, the screen stays with the results and a "Try again" button for items that can still be bought, showing the new cost. No money, no space and trader limits are not retried.
- "Retry sold-out offers" can be turned off on the review screen (or in F12) to choose Retry or Skip yourself. Items bought on a retry say so, with the price change.
- When an offer sells out mid-purchase, a replacement is only bought automatically if it costs at most 10% more (setting "Retry price limit %"). Pricier ones wait for Try again.
- After buying, problems are listed first. Hover a failed item to retry or skip just that one. Bought items show what was paid, and each section shows the real total.
- The main button now says "Buy N items". When money is short it says how much is missing, and the money card shows every currency the purchase needs.
- List rows show the price per item and where it comes from (trader name or flea market).
- Areas you can upgrade right now get a "Ready to upgrade" tag and move to the top.
- "Find in raid" is amber instead of red, and hovering any status explains it.
- Each area has a level track: click a level to buy for every upgrade up to it, or click the next level again to skip the area. Planned levels are listed under their area, and locked levels say what they need. This replaces the area checkboxes and "Show future upgrades".
- Areas show their hideout icon, what their planned levels cost, and what a locked level still needs. Skipped areas fold to their header.
- Reset button to go back to the starting plan, and "All available" to plan the next level of every area that is not locked.
- Areas left out because their next level is locked say "locked" instead of "skipped" and sit at the bottom.
- Dollars and euros are bought from traders when an upgrade needs them or when items priced in them would leave you short. They are bought first and can be unticked like any item.
- The Christmas Tree only shows while the Christmas event is on.
- Found-in-raid items you can craft say "Craftable" even with Craft ingredients off. Crafts have a "Go to station" button that opens the station.
- Only statuses you act on are filled; the others are outlines. Item names lose the stars other mods add, and their tags and price sit on a line below.
- Click the last planned level again to unplan it.
- Areas you are buying for come first. Areas with nothing to buy say what is left (to find in raid, to craft, not for sale).
- The review screen is split by area, with each area's icon and planned levels. The buy button shows the cost.
- Money cards keep roubles big and show dollars and euros small, so they no longer overflow.
- Items that share a short name (like the Christmas ornaments) show their full name.
- "Craft ingredients" moved to the options row.
- New "Hide ready items" option.
- Unticked items and folded sections are remembered between sessions.
- Esc closes the window.

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

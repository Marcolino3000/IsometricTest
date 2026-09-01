using System.Linq;
using Runtime.Core.Spawning;
using Runtime.Core.State;
using Runtime.Gameplay.Entities;
using Runtime.Gameplay.Global;
using UnityEngine;

namespace Runtime.Gameplay.Feedback
{
    public class TileHighlighter : MonoBehaviour
    {
        [SerializeField] private TileSpawner tileSpawner;
        [SerializeField] private Selector selector;

        private void Awake()
        {
            selector.OnSelectionChanged += HandleSelectionChanged;
        }

        private void HandleSelectionChanged(ChangeEvent<Selection> changeEvent)
        {
            tileSpawner.ResetHighlightedTiles();

            var selection = changeEvent.NewValue;

            switch (selection.Status)
            {
                case SelectionStatus.NoSelectionFriendlyHover:
                    selection.HoveredUnit.TileHighlighter.HighlightMoveableTiles();
                    break;

                // Nothing of the player's is selected, so the whole board is about the hovered enemy:
                // where it can walk, and around that the halo of where it can reach without walking.
                case SelectionStatus.NoSelectionEnemyHover:
                    selection.HoveredUnit.TileHighlighter.HighlightMoveableTiles();
                    selection.HoveredUnit.TileHighlighter.HighlightThreatenedTiles(markReachable: true);
                    break;

                case SelectionStatus.SelectionNoHover:
                case SelectionStatus.SelectionFriendlyHover:
                case SelectionStatus.SelectionEnemyClick:
                case SelectionStatus.SelectionTileClick:
                    selection.SelectedUnit.TileHighlighter.HighlightMoveableTiles();
                    break;

                // The threat goes over the selected unit's own reach rather than under it: the white
                // here is where *you* can go, so a tile that is both is one you can walk into and be
                // shot on, and that is the half worth showing. The attack indicator stays last, so a
                // swing that would also catch its own target still reads as aimed at it.
                case SelectionStatus.SelectionEnemyHover:
                    selection.SelectedUnit.TileHighlighter.HighlightMoveableTiles();
                    selection.HoveredUnit.TileHighlighter.HighlightThreatenedTiles();
                    ShowAreaEffectTiles(selection.SelectedUnit, selection.HoveredUnit);
                    ShowAttackIndicatorTile(selection.HoveredUnit.CurrentState.Position);
                    break;

                case SelectionStatus.SelectionTileHover:
                    selection.SelectedUnit.TileHighlighter.HighlightMoveableTiles();
                    break;
            }
        }

        private void ShowAttackIndicatorTile(Tile tile)
        {
            tileSpawner.HighlightTile(tile.Position, MarkerColor.Orange);
        }

        /// <summary>
        /// The ground a swing would spill onto besides the unit it is aimed at. Asked of
        /// <see cref="CombatRules.AreaEffectTiles"/>, which reads the same selectors the strike
        /// resolves its victims from - so the marked tiles are the shape the effect actually has: it
        /// stops where a shot would, and it is measured from the tile the unit would swing from
        /// rather than the one it stands on, since an area centred on the attacker moves with it.
        ///
        /// The ground rather than the units on it, so the reach reads before anybody is standing in
        /// it. Which of them would actually be caught is still the effect's conditions' business -
        /// a neighbour at full health is inside a marked area and takes nothing while the effect
        /// asks for one already damaged.
        ///
        /// Costs nothing for a weapon that does nothing beyond the blow, which is most of them.
        /// </summary>
        private void ShowAreaEffectTiles(Unit attacker, Unit target)
        {
            if (attacker == null || !attacker.IsAlive || target == null || !target.IsAlive)
                return;

            if (!CombatRules.HasAreaEffects(attacker))
                return;

            var fromTile = tileSpawner.GetAttackApproachPath(attacker, target.CurrentState.Position)
                .LastOrDefault();

            foreach (var tile in CombatRules.AreaEffectTiles(attacker, fromTile, target))
                tileSpawner.HighlightTile(tile, MarkerColor.Red);
        }
    }
}
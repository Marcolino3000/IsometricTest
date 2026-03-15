using System;
using UnityEngine;

namespace Runtime
{
    public class GameStateManager : MonoBehaviour
    {
        [Header("Current State")]
        [SerializeField] private Team CurrentTeam;

        [Header("References")] 
        [SerializeField] private Raycaster raycaster;

        private void Awake()
        {
            Setup();
        }

        private void Setup()
        {
            raycaster.OnTurnFinished += SwitchActiveTeam;
            Direction.SetContext(new Context{ Team = CurrentTeam });
        }

        private void SwitchActiveTeam()
        {
            CurrentTeam = CurrentTeam == Team.Player ? Team.Opponent : Team.Player;
            Direction.SetContext(new Context{ Team = CurrentTeam });
        }
    }
}
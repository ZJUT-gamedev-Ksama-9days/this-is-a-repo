using UnityEngine;
using UnityEngine.Tilemaps;
namespace GravityPrototype
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class CircuitSwitch2D : MonoBehaviour
    {
        [SerializeField] private Collider2D[] barriers;
        [SerializeField] private Tilemap circuit;
        [SerializeField] private TileBase emptyStraight, poweredStraight, emptyCorner, poweredCorner;
        public bool IsPowered { get; private set; }
        private void Awake() { GetComponent<Collider2D>().isTrigger = true; }
        private void OnTriggerEnter2D(Collider2D other)
        {
            var player=other.GetComponentInParent<PlayerController2D>();
            if(player==null || player.InputBlocked || (player.GetComponent<PlayerHealth>() is PlayerHealth h && h.IsDead))return;
            PowerOn();
        }
        public void PowerOn()
        {
            if(IsPowered || Time.timeScale<=0f)return;
            IsPowered=true;Apply();
        }
        public void ResetCircuit() { IsPowered=false;Apply(); }
        private void Apply()
        {
            if(barriers!=null)foreach(var b in barriers)if(b!=null)
            {
                b.enabled=!IsPowered;
                var r=b.GetComponent<SpriteRenderer>();
                if(r!=null){var c=r.color;c.a=IsPowered?.2f:1f;r.color=c;}
            }
            if(circuit==null)return;
            if(IsPowered)
            {
                if(emptyStraight!=null&&poweredStraight!=null)circuit.SwapTile(emptyStraight,poweredStraight);
                if(emptyCorner!=null&&poweredCorner!=null)circuit.SwapTile(emptyCorner,poweredCorner);
            }
            else
            {
                if(emptyStraight!=null&&poweredStraight!=null)circuit.SwapTile(poweredStraight,emptyStraight);
                if(emptyCorner!=null&&poweredCorner!=null)circuit.SwapTile(poweredCorner,emptyCorner);
            }
        }
    }
}

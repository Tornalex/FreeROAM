using UnityEngine;

public partial class Spear : MonoBehaviour
{
    private float _ropeLength;

    private GameObject _rope;

    private bool _canSpawnRope;

    private void CreateRope()
    {
        if (_rope != null)
            return;

        if (!_canSpawnRope)
            return;

        _rope =
            new GameObject("Rope");

        _rope.transform.SetParent(
            transform,
            false
        );

        _rope.transform.localPosition =
            RopeAttachOffset;

        _rope.transform.rotation =
            Quaternion.identity;

        _rope.layer =
            _spearLayer;

        LineRenderer lr =
            _rope.AddComponent<LineRenderer>();

        lr.useWorldSpace =
            false;

        lr.positionCount =
            2;

        lr.SetPosition(
            0,
            Vector3.zero
        );

        lr.SetPosition(
            1,
            Vector3.down *
            _ropeLength
        );

        lr.startWidth =
            RopeWidth;

        lr.endWidth =
            RopeWidth;

        if (RopeMaterial != null)
            lr.sharedMaterial =
                RopeMaterial;

        lr.startColor =
            RopeStartColor;

        lr.endColor =
            RopeEndColor;

        EdgeCollider2D ec =
            _rope.AddComponent<EdgeCollider2D>();

        ec.points =
            new Vector2[]
            {
                Vector2.zero,
                Vector2.down *
                _ropeLength
            };

        ec.isTrigger =
            true;

        _rope.AddComponent<Rope>();
    }

    private void DestroyRope()
    {
        if (_rope != null)
        {
            if (Application.isPlaying)
                Destroy(_rope);
            else
                DestroyImmediate(_rope);

            _rope = null;
        }
    }
}

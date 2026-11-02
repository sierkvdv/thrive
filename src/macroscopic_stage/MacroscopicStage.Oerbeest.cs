using System;
using Godot;

/// <summary>
///   Oerbeest additions to the macroscopic stage: a partner of the player's own species that follows the player,
///   and mating with it (after eating enough) to unlock the next generation in the editor.
/// </summary>
public partial class MacroscopicStage
{
    private const float MatingDistance = 6;
    private const float PartnerFollowDistance = 14;

#pragma warning disable CA2213 // Part of the scene tree, freed with it
    private MacroscopicCreature? oerbeestPartner;
    private Label3D? partnerHeart;
#pragma warning restore CA2213

    private ulong partnerLayoutHash;

    /// <summary>
    ///   For automated tests (DevShots)
    /// </summary>
    public Vector3? DevPartnerPosition =>
        oerbeestPartner != null && IsInstanceValid(oerbeestPartner) ? oerbeestPartner.GlobalPosition : null;
    private float heartPulse;

    private void UpdateOerbeestPartner(double delta)
    {
        if (Player == null || Player.Species.MacroscopicType == MacroscopicSpeciesType.Awakened)
            return;

        if (oerbeestPartner == null || !IsInstanceValid(oerbeestPartner) || oerbeestPartner.IsQueuedForDeletion())
            SpawnOerbeestPartner();

        if (oerbeestPartner == null)
            return;

        // Keep the partner looking like the current species (it changes in the editor)
        var layoutHash = MetaballLayoutHelpers.CalculateLayoutHash(
            (MetaballLayout<MacroscopicMetaball>)Player.Species.ModifiableBodyLayout);

        if (partnerLayoutHash != layoutHash || oerbeestPartner.MovementMode != Player.MovementMode)
        {
            partnerLayoutHash = layoutHash;
            oerbeestPartner.ApplySpecies(Player.Species);
            oerbeestPartner.MovementMode = Player.MovementMode;
        }

        var toPlayer = Player.GlobalPosition - oerbeestPartner.GlobalPosition;
        var flat = toPlayer with { Y = 0 };
        float distance = flat.Length();

        // Follow the player when too far away, so the partner is always easy to find
        if (distance > PartnerFollowDistance)
        {
            oerbeestPartner.MovementDirection = flat.Normalized();
            oerbeestPartner.LookVector = flat.Normalized();
        }
        else
        {
            oerbeestPartner.MovementDirection = Vector3.Zero;
            oerbeestPartner.LookVector = distance > 0.1f ? flat.Normalized() : oerbeestPartner.LookVector;
        }

        if (oerbeestPartner.MovementMode == MovementMode.Swimming)
            oerbeestPartner.SwimTowardsHeight(Player.GlobalPosition.Y, delta);

        // Heart above the partner: big and pulsing while the player is ready to mate
        if (partnerHeart != null)
        {
            heartPulse += (float)delta * (Player.ReadyToMate ? 6 : 2);
            float size = (Player.ReadyToMate ? 1.4f : 0.8f) + MathF.Sin(heartPulse) * 0.15f;
            partnerHeart.Scale = Vector3.One * size;
            partnerHeart.Modulate = Player.HasMated ? new Color(1, 0.85f, 0.3f) : new Color(1, 0.3f, 0.5f);
        }

        if (Player.ReadyToMate && distance < MatingDistance)
        {
            Player.HasMated = true;

            HUD.HUDMessages.ShowMessage(
                "Jullie hebben gepaard! Open de editor om de volgende generatie te ontwerpen.",
                DisplayDuration.Long);

            GD.Print("Oerbeest: player mated with partner");
        }
    }

    private int oerbeestGeneration = 1;

    /// <summary>
    ///   The upstream interactable trees stood right at the spawn and blocked the view; spread them out over the land
    /// </summary>
    private static Vector3 OerbeestTreePosition(Vector3 original)
    {
        var spread = original * 2.2f;
        if (!OerbeestWorld.IsDryLand(spread.X, spread.Z))
            spread = original;

        return spread with { Y = OerbeestWorld.HeightAt(spread.X, spread.Z) };
    }

    private void AnnounceOerbeestGeneration()
    {
        ++oerbeestGeneration;

        double millionYears = (CurrentGame?.GameWorld.TotalPassedTime ?? 0) / 1_000_000;

        HUD.HUDMessages.ShowMessage(
            $"Generatie {oerbeestGeneration} – {millionYears:0} miljoen jaar evolutie. Eet, vind je partner en paar opnieuw.",
            DisplayDuration.ExtraLong);
    }

    private void SpawnOerbeestPartner()
    {
        if (Player == null || CurrentGame == null)
            return;

        var position = Player.GlobalPosition + new Vector3(8, 0, 6);
        if (Player.MovementMode == MovementMode.Walking)
            position.Y = OerbeestWorld.HeightAt(position.X, position.Z) + 0.2f;

        oerbeestPartner = SpawnHelpers.SpawnCreature(Player.Species, position, rootOfDynamicallySpawned,
            SpawnHelpers.LoadMulticellularScene(), true, dummySpawner, CurrentGame);

        oerbeestPartner.MovementMode = Player.MovementMode;
        partnerLayoutHash = MetaballLayoutHelpers.CalculateLayoutHash(
            (MetaballLayout<MacroscopicMetaball>)Player.Species.ModifiableBodyLayout);

        partnerHeart = new Label3D
        {
            Text = "♥",
            FontSize = 160,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Position = new Vector3(0, 4.5f, 0),
            OutlineSize = 24,
            OutlineModulate = new Color(0.2f, 0.05f, 0.1f),
        };

        oerbeestPartner.AddChild(partnerHeart);
    }
}

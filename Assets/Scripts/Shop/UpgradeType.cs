namespace Shop
{
    /// <summary>
    /// The three purchasable stat upgrades (see decision 0011). Explicit values so the enum is a
    /// stable index for <see cref="ShopData"/> / SyncVar levels. weapon swap is post-MVP (not here).
    /// </summary>
    public enum UpgradeType
    {
        Damage = 0,
        MaxHp = 1,
        FireRate = 2,
    }
}

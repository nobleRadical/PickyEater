using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using R2API;
using R2API.Utils;
using RoR2;
using System.Security;
using System.Security.Permissions;
using RoR2.Projectile;
using UnityEngine;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace PickyEaterPlugin
{

    //This attribute specifies that we have a dependency on R2API, as we're using it to add our item to the game.
    //You don't need this if you're not using R2API in your plugin, it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency(R2API.R2API.PluginGUID)]
	
	//This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	
	//We will be using 2 modules from R2API: ItemAPI to add our item and LanguageAPI to add our language tokens.
    [R2APISubmoduleDependency(nameof(ItemAPI), nameof(LanguageAPI))]
	
	//This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class PickyEaterPlugin : BaseUnityPlugin
	{
        //The Plugin GUID should be a unique ID for this plugin, which is human readable (as it is used in places like the config).
        //If we see this PluginGUID as it is on thunderstore, we will deprecate this mod. Change the PluginAuthor and the PluginName !
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "nobleRadical";
        public const string PluginName = "PickyEaterPlugin";
        public const string PluginVersion = "1.0.2";

		//	CONFIG
		//whether scrap gets eaten first, before anything else.
		public static ConfigEntry<bool> scrapsConfig { get; set; }
		private bool scrapsFirst = true;
		public static ConfigEntry<string> peckingConfig { get; set; }
		//the pecking order, in tiers, that egocentrism eats first. including NoTier in this list is kind of a bad idea, because egocentrism won't eat those no matter what.
		private static ItemTier[] peckingOrder = new ItemTier[9]; // = { ItemTier.Lunar, ItemTier.Tier1, ItemTier.Tier2, ItemTier.Tier3, ItemTier.VoidTier1, ItemTier.VoidTier2, ItemTier.VoidTier3, ItemTier.Boss, ItemTier.VoidBoss };

		//all scrap item indicies
		private HG.ReadOnlyArray<ItemIndex> scraps;
		

		//The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
			//Init our logging class so that we can properly log for debugging; done debugging
			Log.Init(Logger);
			scrapsConfig = Config.Bind<bool>("base", "Scraps_First", true, "Should egocentrism prioritize eating scrap first? true or false.");
			peckingConfig = Config.Bind<string>("base", "Pecking_Order", "Lunar,Tier1,Tier2,Tier3,Boss,VoidTier1,VoidTier2,VoidTier3,VoidBoss", "The order of which egocentrism eats item tiers. Make sure to stick to these exact wordings.");
			FindConfig(scrapsConfig, peckingConfig);

            // initialize scraps
			On.RoR2.ItemCatalog.Init += (orig) =>
			{
				orig();
				//add the scraps to scraps
				scraps = ItemCatalog.GetItemsWithTag(ItemTag.Scrap);

			};
			//where the magic happens: when egocentrism takes an item
			On.RoR2.LunarSunBehavior.FixedUpdate += LunarSunBehavior_FixedUpdate;

            
            // This line of log will appear in the bepinex console when the Awake method is done.
            Log.LogInfo(nameof(Awake) + " done.");
        }

        private void FindConfig(ConfigEntry<bool> scrapsConfig, ConfigEntry<string> peckingConfig)
		{
			scrapsFirst = scrapsConfig.Value;
			string[] peckingString = peckingConfig.Value.Split(',');
            for (int i = 0; i < peckingString.Length; i++)
            {
                switch (peckingString[i].ToLower())
                {
					case "lunar":
						peckingOrder[i] = ItemTier.Lunar;
						break;
					case "tier1":
						peckingOrder[i] = ItemTier.Tier1;
						break;
					case "tier2":
						peckingOrder[i] = ItemTier.Tier2;
						break;
					case "tier3":
						peckingOrder[i] = ItemTier.Tier3;
						break;
					case "boss":
						peckingOrder[i] = ItemTier.Boss;
						break;
					case "voidtier1":
						peckingOrder[i] = ItemTier.VoidTier1;
						break;
					case "voidtier2":
						peckingOrder[i] = ItemTier.VoidTier2;
						break;
					case "voidtier3":
						peckingOrder[i] = ItemTier.VoidTier3;
						break;
					case "voidboss":
						peckingOrder[i] = ItemTier.VoidBoss;
						break;
					default:
						Log.LogWarning("String-to-list failure");
						break;
				}
            }

			string v = string.Join(",", peckingOrder);
            Log.LogDebug($"Config Initialized. scrapsFirst: {scrapsFirst}, peckingOrder: {v}");
		}

        private void LunarSunBehavior_FixedUpdate(On.RoR2.LunarSunBehavior.orig_FixedUpdate orig, LunarSunBehavior self)
		{
			{
				self.projectileTimer += Time.fixedDeltaTime;
				if (!self.body.master.IsDeployableLimited(DeployableSlot.LunarSunBomb) && self.projectileTimer > 3f / (float)self.stack)
				{
					self.projectileTimer = 0f;
					FireProjectileInfo fireProjectileInfo = default(FireProjectileInfo);
					fireProjectileInfo.projectilePrefab = self.projectilePrefab;
					fireProjectileInfo.crit = self.body.RollCrit();
					fireProjectileInfo.damage = self.body.damage * 3.6f;
					fireProjectileInfo.damageColorIndex = DamageColorIndex.Item;
					fireProjectileInfo.force = 0f;
					fireProjectileInfo.owner = self.gameObject;
					fireProjectileInfo.position = self.body.transform.position;
					fireProjectileInfo.rotation = Quaternion.identity;
					FireProjectileInfo fireProjectileInfo2 = fireProjectileInfo;
					ProjectileManager.instance.FireProjectile(fireProjectileInfo2);
				}
				self.transformTimer += Time.fixedDeltaTime;
				if (!(self.transformTimer > 60f))
				{
					return;
				}
				self.transformTimer = 0f;
				if (!self.body.master || !self.body.inventory)
				{
					return;
				}
				List<ItemIndex> list = new List<ItemIndex>(self.body.inventory.itemAcquisitionOrder);
				ItemIndex itemIndex = ItemIndex.None;
				(bool ScrapFilter, ItemTier TierFilter) = findFilter(list);
				Util.ShuffleList(list, self.transformRng);
				foreach (ItemIndex item in list)
				{
					if (item != DLC1Content.Items.LunarSun.itemIndex)
					{
						ItemDef itemDef = ItemCatalog.GetItemDef(item);
						if ((bool)itemDef && itemDef.tier != ItemTier.NoTier && itemDef.tier == TierFilter)
						{
							if (ScrapFilter)
                            {
								if (scraps.Contains(item))
                                {
									itemIndex = item;
									break;
                                }
                                else
                                {
									continue;
                                }
                            }
							itemIndex = item;
							break;
						}
					}
				}
				if (itemIndex != ItemIndex.None)
				{
					self.body.inventory.RemoveItem(itemIndex);
					self.body.inventory.GiveItem(DLC1Content.Items.LunarSun);
					CharacterMasterNotificationQueue.PushItemTransformNotification(self.body.master, itemIndex, DLC1Content.Items.LunarSun.itemIndex, CharacterMasterNotificationQueue.TransformationType.LunarSun);
				}
			}
		}
		private (bool ScrapFilter, ItemTier TierFilter) findFilter(List<ItemIndex> oldList) 
		{
			//let's check for scrap (provided the config says to)
			//whether or not we're going to be filtering for scrap
			bool scrapFilter = false;

			if (scrapsFirst)
			{
				// find if there is scrap in the list
				foreach (ItemIndex item in oldList)
				{
					if (scraps.Contains(item))
					{
						scrapFilter = true;
						break;
					}
				}
			}

			//we'll be using a tier filter to take out the tiers we don't want; this is the default value.
			ItemTier filterTier = ItemTier.NoTier;

			//for each item tier
			foreach (ItemTier tier in peckingOrder)
			{
				//check the whole list for that tier; if exists, then set the filter to that tier, break the loop.
				foreach (ItemIndex item in oldList)
				{
					
					if (ItemCatalog.GetItemDef(item).tier == tier && ItemCatalog.GetItemDef(item).name != "LunarSun")
					{
						if (scrapFilter && scraps.Contains(item))
						{
							filterTier = tier;
							break;
						}
						else if (scrapFilter && !scraps.Contains(item))
						{
							continue;
						}
						else
						{
							filterTier = tier;
							break;
						}

					}
				}
				if (filterTier != ItemTier.NoTier)
				{
					break;
				}
			}
			//Log.LogMessage(filterTier);
			//Log.LogMessage(scrapFilter);

			return (ScrapFilter: scrapFilter, TierFilter: filterTier);
		}
	}
}

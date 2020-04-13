using System;
using System.Collections;
using System.Collections.Generic;
using Atmospherics;
using UnityEngine;
using Mirror;
using UnityEngine.Events;
using System.Linq;
using UnityEngine.UI;

public class ItemMagBoots : NetworkBehaviour,
	IServerActionGUI, IClientInventoryMove,IServerInventoryMove
{
	[SyncVar(hook = nameof(SyncIsOn))]private bool isOn = false;

	public GameObject spriteObject;
	private SpriteHandler spriteHandler;

	[Tooltip("For UI button, 0 = off , 1 = on")]
	public Sprite[] sprites;

	public  SpriteSheetAndData[] spriteSheets;

	private ConnectedPlayer player;
	private ItemAttributesV2 itemAttributesV2;
	private Pickupable pick;

	[Tooltip("The speed debuff to apply to run speed.")]
	[SerializeField]
	private float runSpeedDebuff = 1.5f;


	[SerializeField]
	private ActionData actionData = null;
	public ActionData ActionData => actionData;

	private PlayerMove playerMove;

	private void Awake()
	{
		pick = GetComponent<Pickupable>();
		spriteHandler = spriteObject.GetComponent<SpriteHandler>();
		itemAttributesV2 = gameObject.GetComponent<ItemAttributesV2>();
		pick.RefreshUISlotImage();
	}

	public void OnInventoryMoveServer(InventoryMove info)
	{
		if (IsPuttingOn(info))
		{
			playerMove = info.ToRootPlayer.PlayerScript.playerMove;
		}

		else if (IsTakingOff(info) & isOn)
		{
			playerMove = info.FromRootPlayer.PlayerScript.playerMove;
			isOn = !isOn;
			RemoveEffect();
		}
	}

	private static bool IsPuttingOn (InventoryMove info)
	{
		if (info.ToSlot == null | info.ToSlot?.NamedSlot == null)
		{
			return false;
		}
		return info.ToSlot.NamedSlot == NamedSlot.feet;
	}

	private static bool IsTakingOff (InventoryMove info)
	{
		if (info.FromSlot == null | info.FromSlot?.NamedSlot == null)
		{
			return false;
		}
		return info.FromSlot.NamedSlot == NamedSlot.feet;
	}

	private void ApplyEffect()
	{
		itemAttributesV2.AddTrait(CommonTraits.Instance.NoSlip);
		playerMove.ServerChangeSpeed(run: playerMove.RunSpeed - runSpeedDebuff);
		playerMove.PlayerScript.pushPull.ServerSetPushable(false);
	}

	private void RemoveEffect()
	{
		itemAttributesV2.RemoveTrait(CommonTraits.Instance.NoSlip);
		playerMove.ServerChangeSpeed(run: playerMove.RunSpeed + runSpeedDebuff);
		playerMove.PlayerScript.pushPull.ServerSetPushable(true);
	}

	[Server]
	public void ServerChangeState(ConnectedPlayer newPlayer)
	{
		isOn = !isOn;
		player = newPlayer;

		if (isOn)
		{
			ApplyEffect();
		}
		else
		{
			RemoveEffect();
		}
	}

	private void SyncIsOn(bool oldIsOn, bool newIsOn)
	{
		isOn = newIsOn;
		spriteHandler.ChangeSprite(isOn ? 1 : 0);
		pick.RefreshUISlotImage();
	}

	private void OnPlayerDeath()
	{
		if (isServer)
		{
			if (isOn)
			{
				ServerChangeState(player);
			}
			UIActionManager.Toggle(this, false);
			player.Script.playerHealth.OnDeathNotifyEvent -= OnPlayerDeath;
			player = null;
		}
		else
		{
			UIActionManager.Toggle(this, false);
		}
	}

	public void OnInventoryMoveClient(ClientInventoryMove info)
	{
		var pna = PlayerManager.LocalPlayerScript.playerNetworkActions;
		if (info.ClientInventoryMoveType == ClientInventoryMoveType.Added
		    & pna.GetActiveItemInSlot(NamedSlot.feet)?.gameObject == gameObject)
		{
			UIActionManager.Toggle(this, true);
			UIActionManager.SetSprite(this, (sprites[0]));
		}
		else if (info.ClientInventoryMoveType == ClientInventoryMoveType.Removed
		         & pna.GetActiveItemInSlot(NamedSlot.feet)?.gameObject != gameObject)
		{
			UIActionManager.Toggle(this, false);
		}
	}

	public void CallActionClient()
	{
		UIActionManager.SetSprite(this, (!isOn ? sprites[1] : sprites[0]));
	}

	public void CallActionServer(ConnectedPlayer SentByPlayer)
	{
		ServerChangeState(SentByPlayer);
	}
}
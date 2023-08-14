﻿using BB_MOD.Events;
using BB_MOD.Extra;
using BB_MOD.ExtraItems;
using BB_MOD.NPCs;
using HarmonyLib;
using MTM101BaldAPI;
using MTM101BaldAPI.AssetManager;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Networking;
using static BB_MOD.ContentManager;
using static BB_MOD.ContentAssets;

// -------------------- PRO TIP ----------------------
// Recommended using UnityExplorer to debug your item, event or npc. It's a very useful tool
// I also recommend using Github Desktop to update the files when needed, it's much faster than having to replace everything manually
// ------------- Reminder ---------------
// I recommend looking around the code for comments
//			they are useful
// Use CTRL + F and search for "CreateNPC", from there you'll find all the methods for creation of content

namespace BB_MOD
{

	public enum Floors
	{
		None, // This value is reserved and should never be used in an object!
		F1,
		F2,
		F3,
		END
	}

	public enum SchoolTextType
	{
		None,
		Ceiling,
		Wall,
		Floor
	}


	public enum BuilderType
	{
		RandomHallBuilder,
		ForcedObjectBuilder,
		RandomObjectBuilder
	}

	public class WeightedTexture2D_ForRooms
	{
		public WeightedTexture2D_ForRooms(WeightedTexture2D text, SchoolTextType type)
		{
			selection = text;
			this.type = type;
		}

		public WeightedTexture2D_ForRooms(WeightedTexture2D text)
		{
			selection = text;
		}

		public static WeightedTexture2D_ForRooms Create(WeightedTexture2D text, SchoolTextType type) => new WeightedTexture2D_ForRooms(text, type);

		public static WeightedTexture2D_ForRooms Create(Texture2D text, int weight, SchoolTextType type) => new WeightedTexture2D_ForRooms(new WeightedTexture2D() { selection = text, weight = weight }, type);

		public void SetupVariables(SchoolTextType type, RoomCategory room)
		{
			this.type = type;
			this.room = room;
		}

		public WeightedTexture2D selection;

		public SchoolTextType type = SchoolTextType.None;

		public RoomCategory room = RoomCategory.Null;
	}


	public class UnsupportedRoomCategoryException : Exception
	{
		public UnsupportedRoomCategoryException() : base()
		{
		}

		public UnsupportedRoomCategoryException(RoomCategory cat)
		{
			category = cat;
		}

		public override string Message => $"The array or enum is set to an invalid category due to being unsupported by the current operation{(category != RoomCategory.Null ? " (" + category + ")" : "")}";

		readonly RoomCategory category = RoomCategory.Null;
	}

	public static class FloorEnumExtensions
	{
		public static Floors ToFloorIdentifier(this string name)
		{
			switch (name.ToLower()) // Converts Floor name to Floors Enum
			{
				case "f1":
					return Floors.F1;

				case "f2":
					return Floors.F2;

				case "f3":
					return Floors.F3;

				case "end":
					return Floors.END;
				default:
					return Floors.None;
			}
		}

		public static Items GetItemByName(this List<Items> items, string name)
		{
			foreach (var item in items)
			{
				if (EnumExtensions.GetExtendedName<Items>((int)item).ToLower() == name.ToLower())
					return item;
			}
			return Items.None;
		}

		public static Character GetCharacterByName(this List<Character> npcs, string name)
		{
			foreach (var item in npcs)
			{
				if (EnumExtensions.GetExtendedName<Character>((int)item).ToLower() == name.ToLower())
					return item;
			}
			return Character.Null;
		}

		public static bool GetEventByName(this List<RandomEventType> events, string name, out RandomEventType eventType)
		{
			foreach (var item in events)
			{
				if (EnumExtensions.GetExtendedName<RandomEventType>((int)item).ToLower() == name.ToLower())
				{
					eventType = item;
					return true;
				}
			}
			eventType = RandomEventType.Fog;
			return false; // There's no null value, so returns Fog by default
		}

		public static RandomEventType GetEventByName(this List<RandomEventType> events, string name)
		{
			_ = GetEventByName(events, name, out RandomEventType eventType);
			return eventType;
		}

		public static bool GetRoomByName(this List<RoomCategory> rooms, string name, out RoomCategory eventType)
		{
			foreach (var item in rooms)
			{
				if (EnumExtensions.GetExtendedName<RoomCategory>((int)item).ToLower() == name.ToLower())
				{
					eventType = item;
					return true;
				}
			}
			eventType = RoomCategory.Null;
			return false;
		}

		public static RoomCategory GetRoomByName(this List<RoomCategory> rooms, string name)
		{
			_ = GetRoomByName(rooms, name, out RoomCategory eventType);
			return eventType;
		}

		public static Vector2 ToVector2(this Direction dir) => new Vector2[] { new Vector2(0, 1), new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0) }[(int)dir];




		/// <summary>
		/// Replaces an item at <paramref name="index"/> of a List
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values"></param>
		/// <param name="index"></param>
		/// <param name="value"></param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public static void Replace<T>(this IList<T> values, int index, T value)
		{
			if (index < 0 || index >= values.Count || values.Count == 0)
				throw new ArgumentOutOfRangeException($"The index: {index} is out of the list range (Length: {values.Count})");

			values.RemoveAt(index);
			values.Insert(index, value);
		}
		/// <summary>
		/// Does specific action using <paramref name="func"/> set
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values"></param>
		/// <param name="func"></param>
		/// <returns></returns>
		public static IEnumerable<T> DoAndReturn<T>(this IEnumerable<T> values, Func<T, T> func)
		{
			using (var enumerator = values.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					yield return func(enumerator.Current);
				}
			}
		}
		/// <summary>
		/// Extension to remove an item from a collection based on the <paramref name="val"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values"></param>
		/// <param name="val"></param>
		/// <returns>A collection without the item provided</returns>
		/// <exception cref="NullReferenceException"></exception>

		public static IEnumerable<T> RemoveIn<T>(this IEnumerable<T> values, T val)
		{
			return values.Where(x => !ReferenceEquals(x, val) && !Equals(val, x));
		}
		/// <summary>
		/// Extension to remove an item at <paramref name="index"/> from a collection
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values"></param>
		/// <param name="index"></param>
		/// <returns>A collection without the item provided</returns>
		/// <exception cref="NullReferenceException"></exception>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public static IEnumerable<T> RemoveInAt<T>(this IEnumerable<T> values, int index)
		{
			int numeration = 0;
			using (var enumerator = values.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (numeration++ != index)
						yield return enumerator.Current;
				}
			}
		}
		/// <summary>
		/// Extension to find the index of an element inside the collection
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values"></param>
		/// <param name="val"></param>
		/// <returns>The index of the element or -1 if it hasn't been found</returns>
		public static int IndexAt<T>(this IEnumerable<T> values, T val)
		{
			int index = 0;
			using (IEnumerator<T> enumerator = values.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (ReferenceEquals(enumerator.Current, val) || Equals(val, enumerator.Current))
						return index;

					index++;
				}
			}
			return -1;
		}

		/// <summary>
		/// Extension to find the index of the last element inside the collection
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values"></param>
		/// <param name="val"></param>
		/// <returns>The index of the element or -1 if it hasn't been found</returns>
		public static int LastIndexAt<T>(this IEnumerable<T> values, T val)
		{
			int curIndex = -1;
			int index = 0;
			using (IEnumerator<T> enumerator = values.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (ReferenceEquals(enumerator.Current, val) || Equals(val, enumerator.Current))
						curIndex = index;

					index++;
				}
			}
			return curIndex;
		}

		/// <summary>
		/// Extension to find the index of an element inside the collection based on the passed conditions
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values"></param>
		/// <param name="val"></param>
		/// <returns>The index of the element or -1 if it hasn't been found</returns>
		public static int IndexAt<T>(this IEnumerable<T> values, Predicate<T> func)
		{
			int index = 0;
			using (IEnumerator<T> enumerator = values.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (func(enumerator.Current))
						return index;

					index++;
				}
			}
			return -1;
		}

		/// <summary>
		/// Extension to find the index of the last element inside the collection based on the passed conditions
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values"></param>
		/// <param name="val"></param>
		/// <returns>The index of the element or -1 if it hasn't been found</returns>
		public static int LastIndexAt<T>(this IEnumerable<T> values, Predicate<T> func)
		{
			int curIndex = -1;
			int index = 0;
			using (IEnumerator<T> enumerator = values.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					if (func(enumerator.Current))
						curIndex = index;

					index++;
				}
			}
			return curIndex;
		}

		/// <summary>
		/// Gets all childs of the transform based on the name
		/// </summary>
		/// <param name="transform"></param>
		/// <param name="name"></param>
		/// <returns>A Transform[] Array</returns>
		public static List<Transform> GetAllChildsContainingName(this Transform transform, string name)
		{
			if (transform.childCount == 0) return new List<Transform>();

			List<Transform> transforms = new List<Transform>();
			for (int i = 0; i < transform.childCount; i++)
			{
				var t = transform.GetChild(i);
				if (t.name.ToLower().Contains(name.ToLower()))
					transforms.Add(t);
			}
			return transforms;
		}
		/// <summary>
		/// Gets all childs of the transform converted in an array, if no child is found, an empty array is returned
		/// </summary>
		/// <param name="transform"></param>
		/// <returns>A Transform[] Array</returns>
		public static List<Transform> GetAllChilds(this Transform transform)
		{
			if (transform.childCount == 0) return new List<Transform>();

			var transforms = new List<Transform>();
			for (int i = 0; i < transform.childCount; i++)
			{
				transforms.Add(transform.GetChild(i));
			}
			return transforms;
		}
	}

	public static class EnvironmentExtraVariables
	{
		public static void ResetVariables()
		{
			forceDisableSubtitles = false;
			currentFloor = Floors.None;
			ec = null;
			lb = null;
			events.Clear();
			tilesForLighting.Clear();
			belts.Clear();
			completedMachines.Clear();
			allowHangingLights = true;
		}

		/// <summary>
		/// Turn the subtitles off or on
		/// </summary>
		/// <param name="turn"></param>
		public static void TurnSubtitles(bool turn)
		{
			forceDisableSubtitles = !turn;
		}

		public static bool IsPlayerOnLibrary
		{
			get => ec && ec.Players.Length > 0 && ec.TileFromPos(Singleton<CoreGameManager>.Instance.GetPlayer(0).transform.position).room.name.StartsWith("library", StringComparison.OrdinalIgnoreCase);
		}

		public static EnvironmentController ec;

		public static Floors currentFloor;

		public static LevelBuilder lb;

		public static List<RandomEvent> events = new List<RandomEvent>();

		public static Dictionary<BeltManager, float> belts = new Dictionary<BeltManager, float>();

		public static List<MathMachine> completedMachines = new List<MathMachine>();

		private static bool forceDisableSubtitles = false;

		public static bool AreSubtitlesForceDisabled
		{
			get => forceDisableSubtitles;
		}

		// Internal Generator Custom Variables >> Should not be touched in any means

		public static void CreateLighting(TileController tile, int strength, Color mapColor) => tilesForLighting.Add(new LightData(tile, strength, mapColor));

		public static void CreateLighting(TileController tile, Color mapColor) => tilesForLighting.Add(new LightData(tile, lb.ld.standardLightStrength, mapColor));

		public static void CreateLighting(TileController tile, int strength) => tilesForLighting.Add(new LightData(tile, strength, lb.ld.standardLightColor));

		public static void CreateLighting(TileController tile) => tilesForLighting.Add(new LightData(tile, lb.ld.standardLightStrength, lb.ld.standardLightColor));

		public static bool allowHangingLights = true;

		public static List<LightData> tilesForLighting = new List<LightData>();

		public class LightData
		{
			public LightData(TileController tile, int strength, Color mapColor)
			{
				MapColor = mapColor;
				Strength = strength;
				Tile = tile;
			}

			public Color MapColor { get; }

			public int Strength { get; }

			public TileController Tile { get; }
		}
	}

	public static class ContentUtilities
	{
		public static Vector2[] ConvertSideToTexture(int x, int y, int width, int height, int texWidth, int texHeight, int index, Vector2[] oldUv)
		{
			Vector2[] uv = oldUv;
			Vector2[] newUvs = GetUVRectangles(x, y, width, height, texWidth, texHeight);
			int setIdx = index;
			for (int i = 0; i < 4; i++)
			{
				uv[setIdx] = newUvs[i];
				setIdx++;
			}
			return uv;
		}
		private static Vector2 ConvertToUVCoords(int x, int y, int width, int height) => new Vector2((float)x / width, (float)y / height);

		private static Vector2[] GetUVRectangles(int x, int y, int width, int height, int textWidth, int textHeight) => new Vector2[] {
			ConvertToUVCoords(x, y + height, textWidth, textHeight),
			ConvertToUVCoords(x, y, textWidth, textHeight),
			ConvertToUVCoords(x + width, y, textWidth, textHeight),
			ConvertToUVCoords(x + width, y + height, textWidth, textHeight)
		};
		/// <summary>
		/// Creates a non positional audio for set <paramref name="obj"/>
		/// </summary>
		/// <param name="obj"></param>
		public static void CreateNonPositionalAudio(GameObject obj) 
		{ 
			CreatePositionalAudio(obj, 20, 30, out AudioSource source, out _);
			source.spatialBlend = 0f;
		}

		/// <summary>
		/// Creates a positional audio for set <paramref name="obj"/>
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="minDistance"></param>
		/// <param name="maxDistance"></param>
		/// <param name="audio"></param>
		/// <param name="source"></param>
		/// <param name="supportDoppler"></param>
		public static void CreatePositionalAudio(GameObject obj, float minDistance, float maxDistance, bool supportDoppler = false) => CreatePositionalAudio(obj, minDistance, maxDistance, out _, out _, supportDoppler);

		/// <summary>
		/// Creates a positional audio for set <paramref name="obj"/> and returns out the <paramref name="audio"/> and <paramref name="source"/>
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="minDistance"></param>
		/// <param name="maxDistance"></param>
		/// <param name="audio"></param>
		/// <param name="source"></param>
		/// <param name="supportDoppler"></param>
		public static void CreatePositionalAudio(GameObject obj, float minDistance, float maxDistance, out AudioSource audio, out AudioManager source, bool supportDoppler = false)
		{
			var src = obj.AddComponent<AudioSource>();
			var aud = obj.AddComponent<AudioManager>();
			aud.audioDevice = src;
			src.maxDistance = maxDistance;
			src.minDistance = minDistance;
			src.rolloffMode = AudioRolloffMode.Custom;
			src.spatialBlend = 1f;
			src.dopplerLevel = supportDoppler ? 1f : 0f;
			audio = src;
			source = aud;
		}

		/// <summary>
		/// Creates a positional looping audio that plays infinitely (acts like how Playtime's music works)
		/// </summary>
		/// <param name="obj"></param>
		/// <param name="minDistance"></param>
		/// <param name="maxDistance"></param>
		/// <param name="clip"></param>
		/// <param name="supportDoppler"></param>
		public static void CreateMusicManager(GameObject obj, float minDistance, float maxDistance, SoundObject clip, bool supportDoppler = false) // Creates a music like playtime does
		{
			CreatePositionalAudio(obj, minDistance, maxDistance, out _, out AudioManager musicAudMan, supportDoppler);
			musicAudMan.maintainLoop = true;
			musicAudMan.SetLoop(true);
			musicAudMan.QueueAudio(clip);
		}

		public static AudioClip GetAudioClip(string path) // This will temporarily replace the AssetManager.AudioClipFromFile() method, since the method is adding a 1 second delay on the end of the file, making it literally unable to be a loop
		{
			using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file:///" + path, AudioType.WAV))
			{
				www.SendWebRequest();
				while (www.result == UnityWebRequest.Result.InProgress) { }

				if (www.result != UnityWebRequest.Result.Success)
				{
					Debug.LogError("Error loading audio clip: " + www.error);
					return null;
				}

				return DownloadHandlerAudioClip.GetContent(www);
			}
		}

		/// <summary>
		/// Create a basic textured cube object. <paramref name="textureFile"/> refers to png files inside "Textures" folder and must be 256x256
		/// </summary>
		/// <param name="scale"></param>
		/// <param name="textureFile"></param>
		/// <returns>A Cube with Obstacle Navmesh</returns>
		public static Transform CreateBasicCube(Vector3 scale, string textureFile)
		{
			var transform = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;

			transform.localScale = scale;
			Material mat = UnityEngine.Object.Instantiate(FindResourceObjectContainingName<Material>("vent"));
			mat.mainTexture = GetAssetOrCreate<Texture2D>($"basicCubeText_{Path.GetFileNameWithoutExtension(textureFile)}", Path.Combine(modPath, "Textures", textureFile));

			if (mat.mainTexture.height != 256 || mat.mainTexture.width != 256) throw new ArgumentOutOfRangeException("Selected texture for cube has invalid size. Must be 256x256");

			Vector2[] mesh = transform.GetComponent<MeshFilter>().mesh.uv;
			mesh = ConvertSideToTexture(0, 0, 256, 256, 256, 256, 0, mesh);
			mesh = ConvertSideToTexture(0, 0, 256, 256, 256, 256, 4, mesh);
			mesh = ConvertSideToTexture(0, 0, 256, 256, 256, 256, 8, mesh);
			mesh = ConvertSideToTexture(0, 0, 256, 256, 256, 256, 12, mesh);
			mesh = ConvertSideToTexture(0, 0, 256, 256, 256, 256, 16, mesh);
			mesh = ConvertSideToTexture(0, 0, 256, 256, 256, 256, 20, mesh);

			transform.GetComponent<MeshFilter>().mesh.uv = mesh;


			transform.GetComponent<MeshRenderer>().material = mat;

			return transform;
		}
		/// <summary>
		/// Create a basic textured cube object with navmesh obstacle (npcs can't go through it). <paramref name="textureFile"/> refers to png files inside "Textures" folder and must be 256x256
		/// </summary>
		/// <param name="scale"></param>
		/// <param name="textureFile"></param>
		/// <returns>A Cube with Obstacle Navmesh</returns>
		public static Transform CreateBasicCube_WithObstacle(Vector3 scale, Vector3 obstacleSize, string textureFile)
		{
			var transform = CreateBasicCube(scale, textureFile);
			var obs = transform.gameObject.AddComponent<NavMeshObstacle>();
			obs.carving = true;
			obs.size = obstacleSize;
			obs.center = new Vector3(0f, transform.position.y, 0f);
			return transform;
		}

		/// <summary>
		/// Create a basic textured cube object with navmesh obstacle (npcs can't go through it). The size of the obstacle will be the same as the cube's <paramref name="scale"/>. <paramref name="textureFile"/> refers to png files inside "Textures" folder and must be 256x256
		/// </summary>
		/// <param name="scale"></param>
		/// <param name="textureFile"></param>
		/// <returns>A Cube with Obstacle Navmesh</returns>
		public static Transform CreateBasicCube_WithObstacle(Vector3 scale, string textureFile) => CreateBasicCube_WithObstacle(scale, scale, textureFile);

		/// <summary>
		/// Find an object in-game that contains the following <paramref name="name"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <returns>The target object</returns>
		public static T FindObjectContainingName<T>(string name, bool includeDisabled = false) where T : UnityEngine.Object
		{
			return UnityEngine.Object.FindObjectsOfType<T>(includeDisabled).First(x => x.name.ToLower().Contains(name.ToLower()));
		}
		/// <summary>
		/// Find objects in-game that contains the following <paramref name="name"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <returns>An array of the target object</returns>
		public static T[] FindObjectsContainingName<T>(string name, bool includeDisabled = false) where T : UnityEngine.Object
		{
			return UnityEngine.Object.FindObjectsOfType<T>(includeDisabled).Where(x => x.name.ToLower().Contains(name.ToLower())).ToArray();
		}

		/// <summary>
		/// Find an object from the Resources by the <paramref name="name"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <returns>An <typeparamref name="T"/> object from resources</returns>
		public static T FindResourceObjectContainingName<T>(string name) where T : UnityEngine.Object
		{
			return Resources.FindObjectsOfTypeAll<T>().First(x => x.name.ToLower().Contains(name.ToLower()));
		}
		/// <summary>
		/// Find an array of objects from the Resources by the <paramref name="name"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <returns>An array of <typeparamref name="T"/> object from resources</returns>
		public static T[] FindResourceObjectsContainingName<T>(string name) where T : UnityEngine.Object
		{
			return Resources.FindObjectsOfTypeAll<T>().Where(x => x.name.ToLower().Contains(name.ToLower())).ToArray();
		}
		/// <summary>
		/// Find an object from the resources
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns>Desired <typeparamref name="T"/> Object</returns>
		public static T FindResourceObject<T>() where T : UnityEngine.Object
		{
			return Resources.FindObjectsOfTypeAll<T>().First();
		}

		/// <summary>
		/// Find objects from the resources
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns>Desired <typeparamref name="T"/> Objects</returns>

		public static T[] FindResourceObjects<T>() where T : UnityEngine.Object
		{
			return Resources.FindObjectsOfTypeAll<T>();
		}
		/// <summary>
		/// Creates a Random Object Spawner (Example: tables from faculties)
		/// </summary>
		/// <param name="array"></param>
		/// <param name="minAmount"></param>
		/// <param name="maxAmount"></param>
		/// <param name="rotateIncrement"></param>
		/// <param name="center"></param>
		/// <param name="room"></param>
		/// <param name="ec"></param>
		/// <returns>The object spawner</returns>
		public static RandomObjectSpawner CreateSpawner(WeightedTransform[] array, int minAmount, int maxAmount, float rotateIncrement, bool center, RoomController room, EnvironmentController ec)
		{
			var coolSpawner = new GameObject("CustomObjectSpawner");
			var spawner = coolSpawner.AddComponent<RandomObjectSpawner>();
			spawner.SetRange(ec.RealRoomMin(room), ec.RealRoomMax(room));
			spawner.transform.SetParent(room.transform);
			AccessTools.Field(typeof(RandomObjectSpawner), "prefab").SetValue(spawner, array);
			AccessTools.Field(typeof(RandomObjectSpawner), "rotate").SetValue(spawner, true);
			AccessTools.Field(typeof(RandomObjectSpawner), "rotationIncrement").SetValue(spawner, rotateIncrement);
			AccessTools.Field(typeof(RandomObjectSpawner), "center").SetValue(spawner, center);
			AccessTools.Field(typeof(RandomObjectSpawner), "minCount").SetValue(spawner, minAmount);
			AccessTools.Field(typeof(RandomObjectSpawner), "maxCount").SetValue(spawner, maxAmount);
			return spawner;


		}

		/// <summary>
		/// Creates a Random Object Spawner (Example: tables from faculties)
		/// </summary>
		/// <param name="array"></param>
		/// <param name="minAmount"></param>
		/// <param name="maxAmount"></param>
		/// <param name="center"></param>
		/// <param name="room"></param>
		/// <param name="ec"></param>
		/// <returns>The object spawner</returns>

		public static RandomObjectSpawner CreateSpawner(WeightedTransform[] array, int minAmount, int maxAmount, bool center, RoomController room, EnvironmentController ec)
		{
			var coolSpawner = new GameObject("CustomObjectSpawner");
			var spawner = coolSpawner.AddComponent<RandomObjectSpawner>();
			spawner.SetRange(ec.RealRoomMin(room), ec.RealRoomMax(room));
			spawner.transform.SetParent(room.transform);
			AccessTools.Field(typeof(RandomObjectSpawner), "prefab").SetValue(spawner, array);
			AccessTools.Field(typeof(RandomObjectSpawner), "center").SetValue(spawner, center);
			AccessTools.Field(typeof(RandomObjectSpawner), "minCount").SetValue(spawner, minAmount);
			AccessTools.Field(typeof(RandomObjectSpawner), "maxCount").SetValue(spawner, maxAmount);
			return spawner;
		}

		/// <summary>
		/// Creates a Random Object Spawner (Example: tables from faculties)
		/// </summary>
		/// <param name="transform"></param>
		/// <param name="minAmount"></param>
		/// <param name="maxAmount"></param>
		/// <param name="center"></param>
		/// <param name="room"></param>
		/// <param name="ec"></param>
		/// <returns>The object spawner</returns>

		public static RandomObjectSpawner CreateSpawner(WeightedTransform transform, int minAmount, int maxAmount, bool center, RoomController room, EnvironmentController ec)
		{
			return CreateSpawner(Array(transform), minAmount, maxAmount, center, room, ec);
		}

		/// <summary>
		/// Creates a Random Object Spawner (Example: tables from faculties)
		/// </summary>
		/// <param name="transform"></param>
		/// <param name="minAmount"></param>
		/// <param name="maxAmount"></param>
		/// <param name="rotateIncrement"
		/// <param name="center"></param>
		/// <param name="room"></param>
		/// <param name="ec"></param>
		/// <returns>The object spawner</returns>

		public static RandomObjectSpawner CreateSpawner(WeightedTransform transform, int minAmount, int maxAmount, float rotateIncrement, bool center, RoomController room, EnvironmentController ec)
		{
			return CreateSpawner(new WeightedTransform[] { transform }, minAmount, maxAmount, rotateIncrement, center, room, ec);
		}

		/// <summary>
		/// Replacement for PlaceObject method from RoomBuilder, to place an object on the exact position and not with a forced offset. You can also set to sinalize whether the tile has an object or wall object with <paramref name="sinalizeObject"/>/<paramref name="sinalizeWallObject"/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="prefab"></param>
		/// <param name="bld"></param>
		/// <param name="localPosition"></param>
		/// <param name="rotation"></param>
		/// <param name="sinalizeObject"></param>
		/// <param name="sinalizeWallObject"></param>
		/// <returns>The object spawned</returns>
		public static T PlaceObject_RawPos<T>(T prefab, RoomBuilder bld, Vector3 localPosition, Quaternion rotation, bool sinalizeObject = false, bool sinalizeWallObject = false) where T : Component
		{
			var t = UnityEngine.Object.Instantiate(prefab, (Transform)AccessTools.Field(typeof(RoomBuilder), "objectsTransform").GetValue(bld));
			t.transform.localPosition = localPosition + (localPosition.y < 5f ? (Vector3.up * 5f) : Vector3.zero);
			t.transform.rotation = rotation;
			var tile = EnvironmentExtraVariables.ec.TileFromPos(t.transform.position);
			tile.containsObject = sinalizeObject;
			tile.containsWallObject = sinalizeWallObject;
			return t;
		}


		public static PosterTextData[] ConvertPosterTextData(PosterTextData[] source, string keyForPosterName, string keyForPoster) // Gets the Poster Text Data[] and converts it to a new one, so it doesn't get by reference (I hate this)
		{
			var newPosterTextData = new PosterTextData[source.Length];
			for (int i = 0; i < source.Length; i++)
			{
				newPosterTextData[i] = new PosterTextData()
				{
					position = source[i].position,
					alignment = source[i].alignment,
					style = source[i].style,
					color = source[i].color,
					font = source[i].font,
					fontSize = source[i].fontSize,
					size = source[i].size
				};

			}
			newPosterTextData[0].textKey = keyForPosterName;
			newPosterTextData[1].textKey = keyForPoster;

			return newPosterTextData;
		}

		public static PosterTextData CreateTextData(string textKey, int fontSize, IntVector2 textSize, IntVector2 position, FontStyles style, TextAlignmentOptions alignment, Color textColor)
		{
			return new PosterTextData
			{
				textKey = textKey,
				font = instance.posterPre.textData[0].font,
				style = style,
				size = textSize,
				fontSize = fontSize,
				alignment = alignment,
				position = position,
				color = textColor
			};
		}

		public static void AddCollisionToSprite(GameObject obj, Vector3 boxSize, Vector3 center)
		{
			if (obj.GetComponent<BoxCollider>()) return; // Stops it from adding twice

			var collision = obj.AddComponent<BoxCollider>();
			collision.center = center;
			collision.size = boxSize;
		}

		public static void AddCollisionToSprite(GameObject obj, Vector3 boxSize, Vector3 center, Vector3 obstacleSize)
		{
			if (!obj.GetComponent<BoxCollider>()) AddCollisionToSprite(obj, boxSize, center); // If don't exist, add it

			if (obj.GetComponent<NavMeshObstacle>()) return; // Stops it from adding twice

			var obs = obj.AddComponent<NavMeshObstacle>();
			obs.carving = true;
			obs.size = obstacleSize;
			obs.center = new Vector3(0f, obj.transform.position.y, 0f);
		}

		public static void CoverAllWalls(this TileController tile)
		{
			foreach (Direction wall in tile.wallDirections)
			{
				tile.CoverWall(wall, true);
			}
		}

		public static T[] Array<T>(params T[] vals) => vals;


		public static IntVector2 Right
		{
			get => new IntVector2(1, 0);
		}
		public static IntVector2 Up
		{
			get => new IntVector2(0, 1);
		}
		public static IntVector2 Left
		{
			get => new IntVector2(-1, 0);
		}
		public static IntVector2 Down
		{
			get => new IntVector2(0, -1);
		}
		/// <summary>
		/// Returns all available floors
		/// </summary>
		public static Floors[] AllFloors => Array(Floors.F1, Floors.F2, Floors.F3, Floors.END);
		/// <summary>
		/// Returns all available floors but excluding floors inside the <paramref name="exceptions"/>
		/// </summary>
		/// <param name="exceptions"></param>
		/// <returns></returns>
		public static Floors[] AllFloorsExcept(params Floors[] exceptions) => Array(Floors.F1, Floors.F2, Floors.F3, Floors.END).Except(exceptions).ToArray();

		/// <summary>
		/// Returns all the BB+ room categories (that are useful), being Classrooms, Faculties and Offices
		/// </summary>
		public static RoomCategory[] AllUsefulBaldiCategories => Array(RoomCategory.Class, RoomCategory.Faculty, RoomCategory.Office);

		/// <summary>
		/// Returns all the BB+ room categories
		/// </summary>
		public static RoomCategory[] AllBaldiCategories => AllUsefulBaldiCategories.AddRangeToArray(Array(RoomCategory.Test, RoomCategory.FieldTrip, RoomCategory.Mystery));

		/// <summary>
		/// Returns all the BB+ room categories (that are useful) + the custom room categories from the mod
		/// </summary>
		public static RoomCategory[] AllUsefulCategories => AllUsefulBaldiCategories.AddRangeToArray(instance.customRoomEnums.ToArray());
		/// <summary>
		/// Returns all the BB+ room categories + the custom room categories from the mod
		/// </summary>
		public static RoomCategory[] AllCategories => AllBaldiCategories.AddRangeToArray(instance.customRoomEnums.ToArray());
		/// <summary>
		/// Returns the default light prefab used in every room
		/// </summary>
		public static WeightedTransform LightPrefab => EnvironmentExtraVariables.lb.ld.facultyLights[0];

		/// <summary>
		/// Returns an instanced renderer object used by the bsoda to render the billboard
		/// </summary>
		public static GameObject DefaultRenderer => UnityEngine.Object.Instantiate(FindResourceObjectContainingName<SpriteRenderer>("BSODA").gameObject);

		public const float PlayerDefaultWalkSpeed = 16f, PlayerDefaultRunSpeed = 24f;
	}

	/// <summary>
	/// Data base that will store every audio and texture created using the ContentManager class
	/// </summary>
	public static class ContentAssets
	{
		public static void ClearAssetBase()
		{
			audios.Clear();
			textures.Clear();
			sprites.Clear();
		}

		/// <summary>
		/// Creates a texture from <paramref name="path"/> and adds into the database
		/// </summary>
		/// <param name="path"></param>
		/// <param name="assetName"></param>
		/// <exception cref="ArgumentException"></exception>
		public static void AddTextureAsset(string path, string assetName)
		{
			CheckParameters(path, assetName);

			if (!textures.ContainsKey(assetName))
				textures.Add(assetName, AssetManager.TextureFromFile(path));
			else
				Debug.LogWarning("The texture asset: \"" + assetName + "\" already exists on the database");

		}

		/// <summary>
		/// Creates a sprite from <paramref name="path"/> and adds into the database, the <paramref name="pixelsPerUnit"/> sets the size of the sprite texture, larger values make smaller sprites. You can also set the <paramref name="center"/> of the texture, make sure to not set it too off from the object, or it won't even render
		/// </summary>
		/// <param name="path"></param>
		/// <param name="assetName"></param>
		/// /// <exception cref="ArgumentException"></exception>
		public static void AddSpriteAsset(string path, int pixelsPerUnit, string assetName, Vector2 center)
		{
			CheckParameters(path, assetName);


			if (!sprites.ContainsKey(assetName))
				sprites.Add(assetName, AssetManager.SpriteFromTexture2D(AssetManager.TextureFromFile(path), center, pixelsPerUnit));
			else
				Debug.LogWarning("The sprite asset: \"" + assetName + "\" already exists on the database");

		}

		/// <summary>
		/// Creates a sprite from <paramref name="path"/> and adds into the database, the <paramref name="pixelsPerUnit"/> sets the size of the sprite texture, larger values make smaller sprites
		/// </summary>
		/// <param name="path"></param>
		/// <param name="assetName"></param>
		/// /// <exception cref="ArgumentException"></exception>
		public static void AddSpriteAsset(string path, int pixelsPerUnit, string assetName) => AddSpriteAsset(path, pixelsPerUnit, assetName, new Vector2(0.5f, 0.5f));

		/// <summary>
		/// Creates a texture from <paramref name="path"/> and adds into the database, the <paramref name="useDifferentMethod"/> is a temporary parameter, highly recommended if you are going to make looping audios
		/// </summary>
		/// <param name="path"></param>
		/// <param name="assetName"></param>
		/// /// <exception cref="ArgumentException"></exception>
		public static void AddAudioAsset(string path, string assetName, bool useDifferentMethod)
		{
			CheckParameters(path, assetName);

			if (!textures.ContainsKey(assetName))
				audios.Add(assetName, useDifferentMethod ? ContentUtilities.GetAudioClip(path) : AssetManager.AudioClipFromFile(path)); // Gets an audio from either these 2
			else
				Debug.LogWarning("The audio asset: \"" + assetName + "\" already exists on the database");

		}

		private static void CheckParameters(string path, string assetName)
		{
			if (string.IsNullOrEmpty(assetName)) throw new ArgumentException($"The asset name is empty");
			if (!File.Exists(path)) throw new ArgumentException($"No asset found from current path: " + path);
		}
		/// <summary>
		/// Grabs an asset from the database based on <typeparamref name="T"/> type and name <paramref name="name"/>
		/// </summary>
		/// <param name="name"></param>
		/// <returns>Returns the asset found inside the database</returns>
		public static T GetAsset<T>(string name) where T : UnityEngine.Object
		{
			try
			{
				return typeof(T).Equals(typeof(Texture2D)) ? textures[name] as T : typeof(T).Equals(typeof(Sprite)) ? sprites[name] as T : typeof(T).Equals(typeof(AudioClip)) ? audios[name] as T : throw new NotSupportedException("The asset of type " + typeof(T) + " is not supported"); ; // If T equals a specific type, return that type
			}
			catch
			{
				Debug.LogWarning($"Could not find {typeof(T)} asset named \"{name}\" inside the database, returning null");
				return null;
			}
		}
		/// <summary>
		/// Try grabbing an asset from the database based on <typeparamref name="T"/> type and name <paramref name="name"/>, if it fails, it'll create one inside the database using set <paramref name="name"/> (you must also include the parameters inside <paramref name="args"/> for creating it such as pixelsPerUnit for sprites for example) ::
		/// For reference: The required parameters for sprites in order are: pixelsPerUnit (int), Vector2 (optional) || For audios is: useDifferentMethod (bool)
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <param name="path"></param>
		/// <param name="args"></param>
		/// <returns>Returns the asset found inside or directly created inside the database</returns>
		public static T GetAssetOrCreate<T>(string name, string path, params object[] args) where T : UnityEngine.Object
		{
			try
			{
				return typeof(T).Equals(typeof(Texture2D)) ? textures[name] as T : typeof(T).Equals(typeof(Sprite)) ? sprites[name] as T : typeof(T).Equals(typeof(AudioClip)) ? audios[name] as T : throw new NotSupportedException("The asset of type " + typeof(T) + " is not supported"); ; // If T equals a specific type, return that type
			}
			catch
			{
				var objs = new Dictionary<string, UnityEngine.Object>();
				if (typeof(T).Equals(typeof(Texture2D)))
				{
					AddTextureAsset(path, name);
					objs = textures.ToDictionary(x => x.Key, x => (UnityEngine.Object)x.Value);
				}
				else if (typeof(T).Equals(typeof(Sprite)))
				{
					
					if (args.Length > 1)
						AddSpriteAsset(path, (int)args[0], name, (Vector2)args[1]);
					if (args.Length > 1)
						AddSpriteAsset(path, (int)args[0], name);
					objs = sprites.ToDictionary(x => x.Key, x => (UnityEngine.Object)x.Value);
				}
				else if (typeof(T).Equals(typeof(AudioClip)))
				{
					AddAudioAsset(path, name, (bool)args[0]);
					objs = audios.ToDictionary(x => x.Key, x => (UnityEngine.Object)x.Value);
				}
				else
				{
					throw new NotSupportedException("The asset of type " + typeof(T) + " is not supported");
				}
				return objs[name] as T;
			}
		}

		readonly static Dictionary<string, AudioClip> audios = new Dictionary<string, AudioClip>();

		readonly static Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

		readonly static Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
	}

	public class ContentManager : MonoBehaviour
	{

		private void Awake()
		{
			DontDestroyOnLoad(this);
			instance = this;

			// Add every single BB+ item into the list (default weight being 50 for every one) Note: this list is used later on npcs or mechanics that use item weighted selection

			allItems.AddRange(Resources.FindObjectsOfTypeAll<ItemObject>().ToList().ConvertAll(x => new WeightedItemObject()
			{
				weight = 50,
				selection = x
			}));
		}


		// ----------------- ASSET LOADING -------------------
		// Here you add the assets for whatever content you're gonna add, being: audios, textures and sprites
		// Please put the asset loading scripts on the right section set by the comments below
		// Make sure to comment on the side of the code from what "content" does it belong to or what is it referring to, for organization.
		// Example: AddAudioAsset(...)  // NPC Office Chair | An audio that office chair will use when running through the hallways
		// If you want the content to load any of the assets, you can always use ContentAssets.GetAsset()

		public void SetupAssetData()
		{
			if (assetsLoaded) return;
			assetsLoaded = true;

			// NPC Assets

			AddSpriteAsset(Path.Combine(modPath, "Textures", "npc", "MGS_Magic.png"), 40, "MGS_MagicSprite"); // Sprite of "Magic" for magical student

			

			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "clock_Scream.wav"), "clock_scream", false); // Crazy Clock Audios
			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "clock_tick.wav"), "clock_tick", false);
			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "clock_tack.wav"), "clock_tock", false);
			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "clock_frown.wav"), "clock_frown", false);

			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "ForgottenWarning.wav"), "forgotten_warn", false); // Forgotten Bell Noise

			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "drum_music.wav"), "letsdrum_music", true); // Lets Drum Audios
			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "drum_wannadrum.wav"), "letsdrum_wannadrum", false);
			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "drum_lovetodrum.wav"), "letsdrum_DRUM", true);

			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "MGS_Throw.wav"), "MGS_magic", false); // Magical Student Throw Noise

			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "HappyHolidays.wav"), "HPH_holiday", false); // Happy Holidays saying merry christmas

			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "Superintendent.wav"), "SI_overhere", false); // SuperIntendent when spotting player

			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "0thprize_mustsweep.wav"), "0prize_mustsweep", false); // 0th Prize Noises
			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "0thprize_timetosweep.wav"), "0prize_timetosweep", false);

			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "ChairRolling.wav"), "chair_rolling", true); // Chair rolling noises

			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "PB_Angry0.wav"), "pb_wander1", false); // Pencil Boy Audios
			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "PB_Angry1.wav"), "pb_wander2", false);
			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "PB_Angry2.wav"), "pb_wander3", false);
			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "PB_SeeLaught.wav"), "pb_spot", false);
			AddAudioAsset(Path.Combine(modPath, "Audio", "npc", "PB_EvilLaught.wav"), "pb_catch", false);
			AddAudioAsset(Path.Combine(modPath, "Audio", "item", "pc_stab.wav"), "pb_stab", false);



			// ITEM Assets

			AddAudioAsset(Path.Combine(modPath, "Audio", "item", "bell_bellnoise.wav"), "bellNoise", false); // The bell noise heard when using the bell item
			AddAudioAsset(Path.Combine(modPath, "Audio", "item", "gps_beep.wav"), "gpsBeepNoise", false); // The beep heard when enabling the Global Positional Displayer
			AddAudioAsset(Path.Combine(modPath, "Audio", "item", "prs_unbox.wav"), "presentUnboxing", false);

			// Events Assets

			AddAudioAsset(Path.Combine(modPath, "Audio", "event", "new_CreepyOldComputer.wav"), "fogNewSong", false); // The new noise when the fog event play

			AddAudioAsset(Path.Combine(modPath, "Audio", "event", "blackout_out.wav"), "blackout_off", false); // Blackout event noises
			AddAudioAsset(Path.Combine(modPath, "Audio", "event", "blackout_on.wav"), "blackout_on", false);

			// Misc Assets

			AddSpriteAsset(Path.Combine(modPath, "Textures", "otherMainMenu.png"), 1, "newBaldiMenu"); // The BB Times Main Menu

			AddSpriteAsset(Path.Combine(modPath, "Textures", "npc", "old_sweep.png"), 20, "oldSweepSprite"); // Old Sweep sprite
			AddTextureAsset(Path.Combine(modPath, "Textures", "npc", "pri_oldsweep.png"), "oldSweepPoster"); // Old Sweep Poster

			for (int i = 0; i < 9; i++)
			{
				AddSpriteAsset(Path.Combine(modPath, "Textures", $"bal{i + 10}.png"), 29, "bal" + i + 10);
			}

			AddTextureAsset(Path.Combine(modPath, "Textures", "ventAtlas.png"), "ventAtlasText"); // Texture used by the vent

			AddAudioAsset(Path.Combine(modPath, "Audio", "ventNoise.wav"), "ventNoises", true); // Vent Noises

			AddSpriteAsset(Path.Combine(modPath, "Textures", "balExplode.png"), 29, "balExploding");

		}

		// ------------------------------------------------------ NPC CREATION STUFF ------------------------------------------------------

		public void SetupWeightNPCValues()
		{
			if (addedNPCs)
				return;

			// THIS IS THE PART WHERE YOU PUT YOUR CUSTOM CHARACTER
			// Add the custom npc to the list using the CreateNPC<C> method as seen below (C stands for Character Class, which is the class the NPC will use)

			// Parameters explained in order:
			// name > Name of character (will also be the Character enum name, but without spaces)
			// weight > Spawn Weight/Chance of character (100 is enough, above that will make it spawn in pratically every seed)
			// Weight Info: Npcs mostly have weights between 75 - 100, you can set below 75, but depending on the floor and the amount of potential characters, putting low values such as 25 for F3 for example, can make the character almost impossible to spawn
			// spriteFileName > All File Names of Textures that should be added to the npc (should be png), the first filename on the array will be the default texture
			// flatSprite > if the sprite of the NPC doesn't use billboard (As chalkles does when he is on a chalkboard, for example), if you want to switch between materials, the NPC_CustomData component already have a method for that
			// includeAnimator > Include the animator component (Do whatever you want with it lol)
			// pixelsPerUnit > Basically if this value is larger, the NPC's sprite is smaller
			// spriteYOffset > the offset of the sprite being rendered (if it passes the ground or goes too above, you can regulate by using this variable)
			// posterFileName > file name of the character's detention poster (should also be png), use the placeholder poster from the textures folder to make your own poster
			// keyForPosterName > The json key that is used for the name of the character (basically where the captions come from), go to Language/English/npcCaps and include your own there
			// keyForPOster > The json key that is used for the description of the character, same as above ^^
			// floor > array of floors that the npc can spawn
			// roomsAllowedToSpawn (Optional) > sets the rooms the npc will be able to spawn on (Only hallway by default)
			// hasLooker (Optional, On by default) > If the npc includes a looker component (if it doesn't use the component, you can always disable to not waste resources)
			// enterRooms (Optional, On by default) > If the npc is able of getting inside rooms
			// Aggored (Optional, off by default) > Basically if the Npc's navigator can be overrided (used by Party Event for instance, to bring every npc to the office)
			// ignoreBelts (Optional, off by default) > If the npc ignores conveyor belts
			// capsuleRadius (Optional, default value of 2f) > Set a "size" to the collider of the npc, values higher than 4 are not recommended
			// usingWanderRounds (Optional, off by default) > If the npc uses the method WanderRounds() instead of WanderRandom() on it's navigator, the reason is because WanderRounds uses a heat map, which is disabled by default
			// forceSpawn (optional, off by default) > If the npc ignores the Player's presence and spawn directly (like Gotta Sweep does)
			// isStatic (optional, off by default) > if your npc is static like chalkles or bully for example, mark this on to not get affected by *stuff* that interacts with npcs

			// What is CreateReplacementNPC<C>?
			// In case you're wondering, it has the exact same function as CreateNPC, the only main difference is: it does not spawn directly from the generator, instead, you can choose in an array of characters for it to replace)
			// This is useful for example: if you want to add a character that functions like Gotta Sweep, you can make it spawn but replacing Gotta Sweep since sweep has a closet anyways

			// CharactersToReplace > As the name suggests, an array of characters that the npc can replace
			// existingCharacterEnum (optional0 > if your character will use another character's enum, this is useful for events for example since they search by the Character enum


			// CreateNPC methods should be put here:

			allNpcs.Add(CreateNPC<OfficeChair>("Office Chair", 35, ContentUtilities.Array("officechair.png", "officechair_disabled.png"), false, false, 18f, -0.8f, "pri_ofc.png", "PST_OFC_Name", "PST_OFC_Desc", ContentUtilities.Array(Floors.F1, Floors.END), ContentUtilities.Array(RoomCategory.Faculty, RoomCategory.Office), hasLooker: false, aggored: true, capsuleRadius: 4f, forceSpawn: true)); // PixelGuy
			allNpcs.Add(CreateNPC<HappyHolidays>("Happy Holidays", 15, ContentUtilities.Array("happyholidays.png"), false, false, 70f, -1.5f, "pri_hapho.png", "PST_HapH_Name", "PST_HapH_Desc", ContentUtilities.Array(Floors.F1), enterRooms: false, capsuleRadius: 3f)); // PixelGuy
			allNpcs.Add(CreateNPC<SuperIntendent>("Super Intendent", 75, ContentUtilities.Array("Superintendent.png"), false, false, 50f, -1f, "pri_SI.png", "PST_SI_Name", "PST_SI_Desc", ContentUtilities.Array(Floors.F2, Floors.END), usingWanderRounds: true)); // PixelGuy
			allNpcs.Add(CreateNPC<CrazyClock>("Crazy Clock", 95, ContentUtilities.Array( // All clock sprites in order
			"ClockGuy_Normal_Tick1.png", "ClockGuy_Normal_Tock1.png",
				"ClockGuy_Normal_Tick2.png", "ClockGuy_Normal_Tock2.png",
				"ClockGuy_Sight_Tick1.png", "ClockGuy_Sight_Tock1.png",
				"ClockGuy_Sight_Tick2.png", "ClockGuy_Sight_Tock2.png",
				"ClockGuy_Frown.png",
				"ClockGuy_Scream_Tick.png", "ClockGuy_Scream_Tock.png",
			"ClockGuy_Hide1.png", "ClockGuy_Hide2.png", "ClockGuy_Hide3.png", "ClockGuy_Hide4.png", "ClockGuy_Hide5.png", "ClockGuy_Hide6.png", "ClockGuy_Hide7.png", "ClockGuy_Hide8.png", "ClockGuy_Hide9.png", "ClockGuy_Hide10.png", "ClockGuy_Hide11.png") // Hiding Animation
			, true, false, 30f, 0f, "pri_crazyclock.png", "PST_CC_Name", "PST_CC_Desc", ContentUtilities.Array(Floors.F3), ContentUtilities.Array(RoomCategory.FieldTrip, RoomCategory.Test), forceSpawn: true, aggored: true, ignoreBelts: true, isStatic:true)); // Poolgametm (Coded by PixelGuy)
			allNpcs.Add(CreateNPC<Forgotten>("Forgotten", 40, ContentUtilities.Array("forgotten.png"), false, false, 25f, 0f, "pri_forgotten.png", "PST_Forgotten_Name", "PST_Forgotten_Name_Desc", ContentUtilities.Array(Floors.F2, Floors.F3, Floors.END), enterRooms: true, capsuleRadius: 4f)); // JDvideosPR
			allNpcs.Add(CreateNPC<LetsDrum>("Let's Drum", 45, ContentUtilities.Array("Lets_Drum.png"), false, false, 51f, -1f, "pri_letsdrum.png", "PST_DRUM_Name", "PST_DRUM_Desc", ContentUtilities.Array(Floors.F2, Floors.F3), enterRooms: false)); // PixelGuy
			allNpcs.Add(CreateNPC<Robocam>("Robocam", 65, ContentUtilities.Array("robocam.png"), false, false, 10f, 0f, "pri_robocam.png", "PST_Robocam_Name", "PST_Robocam_Name_Desc", ContentUtilities.Array(Floors.F3, Floors.END), enterRooms: false)); // JDvideosPR
			allNpcs.Add(CreateNPC<PencilBoy>("Pencil Boy", 50, ContentUtilities.Array("pb_angry.png", "pb_angrySpot.png", "pb_happy.png"), false, false, 65f, -1.75f, "pri_pb.png", "PST_PB_Name", "PST_PB_Desc", ContentUtilities.Array(Floors.F2, Floors.END), ContentUtilities.Array(RoomCategory.Hall, RoomCategory.Test), enterRooms: false, capsuleRadius: 2.6f));

			// Replacement NPCs here
			allNpcs.Add(CreateReplacementNPC<ZeroPrize>("0th Prize", 75, ContentUtilities.Array("0thprize_sleep.png", "0thprize.png"), false, false, 50f, -0.5f, 
				"pri_0thprize.png", "PST_0TH_Name", "PST_0TH_Desc", ContentUtilities.Array(Floors.F3), ContentUtilities.Array(Character.Sweep), forceSpawn: true, capsuleRadius:4f, enterRooms:false, hasLooker:false)); // PixelGuy
			allNpcs.Add(CreateReplacementNPC<MagicalStudent>("Magical Student", 35, ContentUtilities.Array("MGS_Throw1.png", "MGS_Throw2.png", "MGS_Throw3.png"), false, false, 60f, -1.6f,
				"pri_MGS.png", "PST_MGS_Name", "PST_MGS_Desc", ContentUtilities.Array(Floors.F3, Floors.END), ContentUtilities.Array(Character.Principal), usingWanderRounds:true)); // TheEnkoder (Coded by PixelGuy)


			// End of Character Spawns

			addedNPCs = true; // To not repeat instancing again
		}


		private WeightedNPC CreateNPC<C>(out bool success, string name, int weight, string[] spritesFileName, bool flatSprite, bool includeAnimator, float pixelsPerUnit, float spriteYOffset, string posterFileName, string keyForPosterName, string keyForPoster, Floors[] floor, bool hasLooker = true, bool enterRooms = true, bool aggored = false, bool ignoreBelts = false, float capsuleRadius = 0f, bool usingWanderRounds = false, bool forceSpawn = false, Character c = Character.Null, bool isStatic = false) where C : NPC // The order of everything here must be IN THE ORDER I PUT, or else it'll log annoying null exceptions
		{
			var cBean = Instantiate(beans); // Instantiate a bean instance and customize it
			cBean.name = "CustomNPC_" + name;
			try
			{
				// NOTE: Default Method will let custom npc spawn only in hallway

				CheckForParameters(weight, floor);

				Destroy(cBean.GetComponent<Beans>()); // Removes beans component, useless


				var customData = cBean.AddComponent<CustomNPCData>();
				Character cEnum = c;
				if (c == Character.Null) cEnum = EnumExtensions.ExtendEnum<Character>(name);
				customNPCEnums.Add(cEnum);
				customData.MyCharacter = cEnum;
				customData.EnterRooms = enterRooms;
				customData.Aggroed = aggored;
				customData.IgnoreBelts = ignoreBelts;
				customData.useHeatMap = usingWanderRounds;
				customData.forceSpawn = forceSpawn;
				List<Sprite> sprites = new List<Sprite>();
				spritesFileName.Do(x => sprites.Add(AssetManager.SpriteFromTexture2D(AssetManager.TextureFromFile(Path.Combine(modPath, "Textures", "npc", x)), new Vector2(0.5f, 0.5f), pixelsPerUnit)));

				customData.sprites = sprites.ToArray(); // Array of sprites can be accessed through CustomNPCData Component

				DontDestroyOnLoad(cBean);

				var beanPoster = beans.GetComponent<Beans>().Poster; // Get beans poster to set data

				customData.poster = ObjectCreatorHandlers.CreatePosterObject(AssetManager.TextureFromFile(Path.Combine(modPath, "Textures", "npc", posterFileName)), beanPoster.material, ContentUtilities.ConvertPosterTextData(beanPoster.textData, keyForPosterName, keyForPoster));

				cBean.AddComponent<C>(); // Adds main component: Custom NPC

				cBean.GetComponent<Animator>().enabled = includeAnimator;
				cBean.GetComponent<Looker>().enabled = hasLooker;


				cBean.SetActive(false);
				cBean.GetComponent<C>().enabled = true;



				cBean.GetComponent<C>().spriteBase = cBean.transform.Find("SpriteBase").gameObject;

				var sprite = cBean.GetComponent<C>().spriteBase.transform.Find("Sprite");

				sprite.localPosition = new Vector3(0f, 0f + spriteYOffset, 0f); // Sets offset

				var renderer = sprite.GetComponent<SpriteRenderer>();
				renderer.sprite = customData.sprites[0]; // Adds custom sprite

				customData.spriteObject = renderer; // Sets the renderer to be used

				cBean.GetComponent<C>().spawnableRooms = new List<RoomCategory>() { RoomCategory.Hall };
				cBean.GetComponent<C>().baseTrigger = ContentUtilities.Array<Collider>(cBean.GetComponent<CapsuleCollider>());

				customData.materials[0] = renderer.material;
				customData.materials[1] = Instantiate(Resources.FindObjectsOfTypeAll<Material>().First(x => x.name.ToLower() == "chalkles"));


				customData.SwitchMaterials(flatSprite); // Gets chalkles material which has no billboard


				npcPair.Add(floor);
				if (isStatic) staticNpcs.Add(cEnum);

				if (capsuleRadius > 0)
					cBean.GetComponent<CapsuleCollider>().radius = capsuleRadius;
				success = true;
				return new WeightedNPC()
				{
					weight = weight,
					selection = cBean.GetComponent<C>()
				};
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Debug.LogWarning($"Looks like a parameter is wrong on the NPC: {name}, please go back to your method and make sure every file path/name is correct!");
				Debug.LogWarning("Your NPC will be destroyed, and a disabled Beans Instance will be added to the list");
			}
			Destroy(cBean);
			npcPair.Add(Array.Empty<Floors>());
			success = false;
			return new WeightedNPC()
			{
				weight = 1,
				selection = beans.GetComponent<Beans>()
			};

		}

		private WeightedNPC CreateNPC<C>(string name, int weight, string[] spriteFileName, bool flatSprite, bool includeAnimator, float pixelsPerUnit, float spriteYOffset, string posterFileName, string keyForPosterName, string keyForPoster, Floors[] floor, RoomCategory[] roomsAllowedToSpawn, bool hasLooker = true, bool enterRooms = true, bool aggored = false, bool ignoreBelts = false, float capsuleRadius = 0f, bool usingWanderRounds = false, bool forceSpawn = false, bool isStatic = false) where C : NPC
		{
			var npc = CreateNPC<C>(out bool _, name, weight, spriteFileName, flatSprite, includeAnimator, pixelsPerUnit, spriteYOffset, posterFileName, keyForPosterName, keyForPoster, floor, hasLooker, enterRooms, aggored, ignoreBelts, capsuleRadius, usingWanderRounds, forceSpawn, Character.Null, isStatic);
			npc.selection.spawnableRooms = roomsAllowedToSpawn.ToList();
			return npc;
		}

		private WeightedNPC CreateNPC<C>(string name, int weight, string[] spriteFileName, bool flatSprite, bool includeAnimator, float pixelsPerUnit, float spriteYOffset, string posterFileName, string keyForPosterName, string keyForPoster, Floors[] floor, bool hasLooker = true, bool enterRooms = true, bool aggored = false, bool ignoreBelts = false, float capsuleRadius = 0f, bool usingWanderRounds = false, bool forceSpawn = false, bool isStatic = false) where C : NPC
		{
			return CreateNPC<C>(out bool _, name, weight, spriteFileName, flatSprite, includeAnimator, pixelsPerUnit, spriteYOffset, posterFileName, keyForPosterName, keyForPoster, floor, hasLooker, enterRooms, aggored, ignoreBelts, capsuleRadius, usingWanderRounds, forceSpawn, Character.Null, isStatic);
		}

		private WeightedNPC CreateReplacementNPC<C>(string name, int weight, string[] spriteFileName, bool flatSprite, bool includeAnimator, float pixelsPerUnit, float spriteYOffset, string posterFileName, string keyForPosterName, string keyForPoster, Floors[] floor, Character[] charactersToReplace, bool hasLooker = true, bool enterRooms = true, bool aggored = false, bool ignoreBelts = false, float capsuleRadius = 0f, bool usingWanderRounds = false, bool forceSpawn = false, Character character = Character.Null, bool isStatic = false) where C : NPC
		{
			var npc = CreateNPC<C>(out bool success, name, weight, spriteFileName, flatSprite, includeAnimator, pixelsPerUnit, spriteYOffset, posterFileName, keyForPosterName, keyForPoster, floor, hasLooker, enterRooms, aggored, ignoreBelts, capsuleRadius, usingWanderRounds, forceSpawn, character, isStatic);
			if (success)
			{
				npc.selection.gameObject.GetComponent<CustomNPCData>().replacementCharacters = charactersToReplace;
			}
			return npc;
		}



		// ---------------------------------------------- ITEM CREATION ----------------------------------------------

		// Parameters of CreateItem() in order:

		// itemNameKey > key for the name of the item
		// itemDescKey > Item's Description (for store)
		// large/small sprite file > name of the file for large/small sprite, shopPrice > price for shop
		// itemName > name of the item (makes a custom Items enum for it)
		// itemCost > The chances of the item to spawn in that room based in specific parameters, such as how many items does it allow to be in that room or if the room is not connected to a hallway for example
		// ItemCost should have a cost around 20 - 50. Lower values can make it REALLY common and higher values might make it not even spawn at all
		// spawnWeight > chance to spawn (in many situations, such as fieldtrips, faculties, etc.)
		// SpawnFloors (Optional) set the floors the item will be able to spawn (default is all floors, including endless)
		// largerPixelsPerUnit > same as npcs, the pixels for the larger sprite (higher values = smaller sizes)
		// smallPixelsPerUnit > same as the previous one, but for the smaller sprite
		// shoppingFloors > floors that the shop will accept the item (if you don't want to, you can always use Array.Empty<Floors>() )
		// shoppingWeight > chance to appear on shop
		// includeOnMysteryRoom (Optional, disabled by default) > if item should be included on mystery room (uses the same weight used for world spawn), doesn't matter the floor, if there's mystery room, it'll spawn
		// includeOnFieldTrip (Optional, disabled by default) > if item should be included on Field Trip (uses the same weight used for world spawn), ^^ same applies for field trips
		// includeOnParty (Optional, disabled by default) > If item is also included on Party Event (uses the same weight used for world spawn), ^^ same applies

		public void SetupItemWeights()
		{
			if (addedItems)
				return;

			addedItems = true;

			// Item Creation Here

			allNewItems.Add(CreateItem<ITM_Present>("PRS_Name", "PRS_Desc", "present.png", "present.png", "Present", 120, 40, 30, ContentUtilities.Array(Floors.F3), 55, ContentUtilities.Array(Floors.F3), 60, includeOnMysteryRoom: true)); // PixelGuy
			allNewItems.Add(CreateItem<ITM_Hammer>("HAM_Name", "HAM_Desc", "hammer.png", "hammerSmall.png", "Hammer", 30, 35, 25, ContentUtilities.AllFloorsExcept(Floors.F1), 125, ContentUtilities.AllFloors, 60)); // PixelGuy
			allNewItems.Add(CreateItem<ITM_Bell>("BEL_Name", "BEL_Desc", "bell.png", "bell.png", "Bell", 30, 25, 25, ContentUtilities.AllFloors, 125, ContentUtilities.AllFloors, 45, includeOnFieldTrip:true)); // PixelGuy
			allNewItems.Add(CreateItem<ITM_GPS>("GPS_Name", "GPS_Desc", "gps.png", "gpsSmall.png", "GPS", 70, 20, 25, ContentUtilities.Array(Floors.F2, Floors.END), 245, ContentUtilities.Array(Floors.F2, Floors.F3), 30, includeOnPartyEvent:true, includeOnFieldTrip:true)); // PixelGuy
			allNewItems.Add(CreateItem<ITM_Pencil>("PC_Name", "PC_Desc", "Pencil.png", "Pencil.png", "Pencil", 40, 22, 25, ContentUtilities.Array(Floors.F2, Floors.END), 40, ContentUtilities.Array(Floors.F2, Floors.F3), 30, includeOnFieldTrip:true)); // FileName3 (Coded by PixelGuy)
		}


		private WeightedItemObject CreateItem<I>(string itemNameKey, string itemDescKey, string largeSpriteFile, string smallSpriteFile, string itemName, int shopPrice, int itemCost, int spawnWeight, int largerPixelsPerUnit, Floors[] shoppingFloors, int shoppingWeight, bool includeOnMysteryRoom = false, bool includeOnFieldTrip = false, bool includeOnPartyEvent = false) where I : Item
		{
			Items cEnum = EnumExtensions.ExtendEnum<Items>(itemName);
			customItemEnums.Add(cEnum);
			var item = ObjectCreatorHandlers.CreateItemObject(itemNameKey, itemDescKey, AssetManager.SpriteFromTexture2D(AssetManager.TextureFromFile(Path.Combine(modPath, "Textures", "item", smallSpriteFile))), AssetManager.SpriteFromTexture2D(AssetManager.TextureFromFile(Path.Combine(modPath, "Textures", "item", largeSpriteFile)), new Vector2(0.5f, 0.5f), largerPixelsPerUnit), cEnum, shopPrice, itemCost);
			var itemInstance = new GameObject(itemName + "_ItemInstance").AddComponent<I>(); // Creates Item Component from a new game object
			try
			{
				CheckForParameters(ContentUtilities.Array(spawnWeight, itemCost));

				DontDestroyOnLoad(itemInstance.gameObject); // Assure that it won't despawn so it doesn't break the item
				item.item = itemInstance;
				item.name = itemName;
				itemInstance.gameObject.SetActive(false); // No null exceptions (Only if it contains Update() or similar )
				var weightedItem = new WeightedItemObject()
				{
					selection = item,
					weight = spawnWeight
				};
				allItems.Add(weightedItem);
				itemPair.Add(ContentUtilities.AllFloors);

				if (shoppingFloors.Length > 0 && (shoppingFloors[0] != Floors.None || shoppingFloors.Length > 1))
				{
					allShoppingItems.Add(new WeightedItemObject() { selection = item, weight = shoppingWeight }, shoppingFloors);
				}

				if (includeOnMysteryRoom)
					mysteryItems.Add(weightedItem);

				if (includeOnPartyEvent)
					partyItems.Add(weightedItem);

				if (includeOnFieldTrip)
					fieldTripItems.Add(new WeightedItem()
					{
						selection = item,
						weight = spawnWeight
					});

				return weightedItem;
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Debug.LogWarning($"Looks like a parameter is wrong on the Item: {itemName}, please go back to your method and make sure every file path/name is correct!");
				Debug.LogWarning("Your Item will be destroyed");
			}
			Destroy(itemInstance);
			itemPair.Add(Array.Empty<Floors>());
			return new WeightedItemObject()
			{
				weight = 50,
				selection = null
			};
		}


		private WeightedItemObject CreateItem<I>(string itemNameKey, string itemDescKey, string largeSpriteFile, string smallSpriteFile, string itemName, int shopPrice, int itemCost, int spawnWeight, Floors[] spawnFloors, int largerPixelsPerUnit, Floors[] shoppingFloors, int shoppingWeight, bool includeOnMysteryRoom = false, bool includeOnFieldTrip = false, bool includeOnPartyEvent = false) where I : Item
		{
			var item = CreateItem<I>(itemNameKey, itemDescKey, largeSpriteFile, smallSpriteFile, itemName, shopPrice, itemCost, spawnWeight, largerPixelsPerUnit, shoppingFloors, shoppingWeight, includeOnMysteryRoom, includeOnFieldTrip, includeOnPartyEvent);
			itemPair.Replace(itemPair.Count - 1, spawnFloors);
			return item;
		}

		// ---------------------------------------------- EVENT CREATION ----------------------------------------------


		// Parameters of CreateEvent<E>() in order:
		// eventName > name of event (also used to the event enum)
		// eventDescKey > Json key from Language/English/eventCaps.json with the description of the event
		// minEventTime > Minimum time the event will last (a rng chooses a value between the min and max time)
		// maxEventTime > Maximum time the event will last (a rng chooses a value between the min and max time)
		// availableFloors > Floors that the event will be enabled
		// weight > weight/chance for the event to be queued into the floor

		public void SetupEventWeights()
		{
			if (addedEvents)
				return;

			addedEvents = true;

			// Event Creation Here

			allEvents.Add(CreateEvent<PrincipalOut>("PrincipalOut", "Event_PriOut", 40f, 60f, Floors.F2, 75)); // PixelGuy
			allEvents.Add(CreateEvent<BlackOut>("BlackOut", "Event_BlackOut", 60f, 120f, Floors.F3, 45)); // PixelGuy

		}



		private WeightedRandomEvent CreateEvent<E>(string eventName, string eventDescKey, float minEventTime, float maxEventTime, Floors[] availableFloors, int weight) where E : RandomEvent
		{
			try
			{
				CheckForParameters(weight, availableFloors);

				var obj = new GameObject("CustomEv_" + eventName, typeof(E), typeof(CustomEventData));
				var data = obj.GetComponent<CustomEventData>();
				data.eventName = eventName;
				var cEnum = EnumExtensions.ExtendEnum<RandomEventType>(eventName);
				data.myEvent = cEnum;
				customEventEnums.Add(cEnum);
				data.eventDescKey = eventDescKey;
				data.minEventTime = minEventTime;
				data.maxEventTime = maxEventTime;

				eventPair.Add(availableFloors);

				DontDestroyOnLoad(obj);
				obj.SetActive(false);

				return new WeightedRandomEvent()
				{
					selection = obj.GetComponent<E>(),
					weight = weight
				};
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Debug.LogWarning($"The event: {eventName} has failed to be added, that will happen if there\'s an invalid weight");
				Debug.LogWarning("Your event won\'t be added to the level");
				eventPair.Add(Array.Empty<Floors>());

				return new WeightedRandomEvent();
			}
		}

		private WeightedRandomEvent CreateEvent<E>(string eventName, string eventDescKey, float minEventTime, float maxEventTime, Floors availableFloor, int weight) where E : RandomEvent =>
			CreateEvent<E>(eventName, eventDescKey, minEventTime, maxEventTime, ContentUtilities.Array(availableFloor), weight);

		// ------------------------------------ CUSTOM POSTER CREATION ---------------------------------------


			// Parameters of CreateSingle/Multi Poster:
			// textureName > name of the texture used for the poster (must be png, and in Textures/poster folder)
			// weight (default is 100) > sets the chance to the poster to spawn (default is 100 to have the same weight as other posters, community walls uses 10 on weight)
			// -- If your poster contains text --
			// textKey > the key (to read the text) from the posterCaps.json file
			// fontSize > size of the font
			// borderLength > basically where will the the text begin to break lines based from the position the text has been set
			// position > text position
			// fontStyle > style of the font (italic, bold, etc.)
			// alignment > alignment of the text
			// textColor > color of the text
			// -- If it is a multi poster --
			// posters > array of posters (you must use the CreatePosterObject() method to fill the array), the first poster on the array will be the main one

			// You can add the poster either for allposters list (general posters that spawn in hallways) or chalkboards list (the chalkboards that spawn at classrooms)

			// -------- WARNING --------
			// Before adding your posters, make sure that the poster texture has the EXACT SIZE OF 256x256 in both dimensions, or else the game will fail to load your texture and will leave an ugly gray wall

		public void SetupPosterWeights()
		{
			if (addedPosters)
				return;

			addedPosters = true;

			// Single Posters Here

			allPosters.Add(CreateSinglePoster("what.png", 30));
			allPosters.Add(CreateSinglePoster("applePoster.png"));

			// Multi-Posters Here

			allPosters.Add(CreateMultiPoster(new PosterObject[] {
				CreatePosterObject("Comic_BaldiCookie_0.png", "Comic_Cookie0", 12, new IntVector2(200, 30), new IntVector2(-2, 150), FontStyles.Normal, TextAlignmentOptions.Center, Color.black),
				CreatePosterObject("Comic_BaldiCookie_1.png", "Comic_Cookie1", 14, new IntVector2(90, 200), new IntVector2(120, 55), FontStyles.Normal, TextAlignmentOptions.Center, Color.black),
				CreatePosterObject("Comic_BaldiCookie_2.png", "Comic_Cookie2", 16, new IntVector2(200, 30), new IntVector2(10, 150), FontStyles.Normal, TextAlignmentOptions.Center, Color.black),
				CreatePosterObject("Comic_BaldiCookie_3.png", "Comic_Cookie3", 17, new IntVector2(110, 200), new IntVector2(25, 40), FontStyles.Normal, TextAlignmentOptions.Center, Color.black)
			}, 100));

			// Chalkboards here (use allChalkboards.Add() )


		}

		private WeightedPosterObject CreateSinglePoster(string textureName, string textKey, int fontSize, IntVector2 borderLength, IntVector2 position, FontStyles fontStyle, TextAlignmentOptions alignment, Color textColor, int weight = 100) // Create a full single poster
		{
			try
			{
				var text = AssetManager.TextureFromFile(Path.Combine(modPath, "Textures", "poster", textureName));
				CheckForParameters(weight, text, 256, 256);

				var material = posterPre.material.DoAndReturn(x => new Material(x)
				{
					mainTexture = text
				}).ToArray();
				return new WeightedPosterObject()
				{
					selection = ObjectCreatorHandlers.CreatePosterObject(text, material, new PosterTextData[] { ContentUtilities.CreateTextData(textKey, fontSize, borderLength, position, fontStyle, alignment, textColor) }),
					weight = weight
				};
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Debug.LogWarning("Failed to load poster, the poster won\'t be added, make sure the file name is correct!");
			}
			return new WeightedPosterObject();

		}

		private WeightedPosterObject CreateSinglePoster(string textureName, PosterTextData[] texts, int weight = 100)
		{
			try
			{
				var text = AssetManager.TextureFromFile(Path.Combine(modPath, "Textures", "poster", textureName));
				CheckForParameters(weight, text, 256, 256);

				var material = posterPre.material.DoAndReturn(x => new Material(x)
				{
					mainTexture = text
				}).ToArray();
				return new WeightedPosterObject()
				{
					selection = ObjectCreatorHandlers.CreatePosterObject(text, material, texts),
					weight = weight
				};
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Debug.LogWarning("Failed to load poster, the poster won\'t be added, make sure the file name is correct!");
			}
			return new WeightedPosterObject()
			{
				selection = posterPre,
				weight = 0
			};
		}

		public PosterObject CreatePosterObject(string textureName, string textKey, int fontSize, IntVector2 borderLength, IntVector2 position, FontStyles fontStyle, TextAlignmentOptions alignment, Color textColor) => CreateSinglePoster(textureName, textKey, fontSize, borderLength, position, fontStyle, alignment, textColor).selection;
		// Can be used outside (public method)

		public PosterObject CreatePosterObject(string textureName) => CreateSinglePoster(textureName).selection; // Can be used outside (public method)

		private WeightedPosterObject CreateSinglePoster(string textureName, int weight = 100) // Create an only-image poster (Community Walls for example)
		{
			try
			{
				var text = AssetManager.TextureFromFile(Path.Combine(modPath, "Textures", "poster", textureName));

				CheckForParameters(weight, text, 256, 256);

				var material = posterPre.material.DoAndReturn(x => new Material(x)
				{
					mainTexture = text
				}).ToArray();
				return new WeightedPosterObject()
				{
					selection = ObjectCreatorHandlers.CreatePosterObject(text, material, Array.Empty<PosterTextData>()),
					weight = weight
				};
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Debug.LogWarning("Failed to load poster, the poster won\'t be added, make sure the file name is correct!");
			}
			return new WeightedPosterObject()
			{
				selection = posterPre,
				weight = 0
			};
		}

		private WeightedPosterObject CreateMultiPoster(PosterObject[] posters, int weight = 100) // Creates posters like those comics
		{
			if (posters.Any(x => !x))
			{
				return new WeightedPosterObject()
				{
					selection = posterPre,
					weight = 0
				};
			}
			posters[0].multiPosterArray = posters;
			return new WeightedPosterObject()
			{
				selection = posters[0],
				weight = weight
			};
		}


		// ------------------ WORLD MAP CREATION -------------------

		// Parameters of CreateSchoolTexture()
		// textureName > name of the texture (png file) to be loaded into the game (must be on Textures/schooltext)
		// floors > floors that the texture will be available
		// SchoolTextType (optional: more than 1 type) > the type of texture it is going to be, can be a wall texture, floor or ceiling texture
		// existOnClassrooms > if the texture also applies for classrooms (off by default)
		// existOnFaculties > if the texture also applies for faculties (off by default), also affects the office textures (office uses the same texture that faculties does)
		// roomsOnly > if the texture is only applied for the rooms (off by default)
		// weight > the chance of the texture being chosen (100 by default, to be equal to the other weights)

		// ---- WARNING ----
		// Just like the poster warning, the wall/ground/ceiling textures must be of the exact: 128x128 on both dimensions!

		public void SetupSchoolTextWeights()
		{
			if (addedTexts)
				return;

			addedTexts = true;

			// Add your textures here

			CreateSchoolTexture("lightCarpet.png", ContentUtilities.AllFloors, SchoolTextType.Floor, existOnClassrooms: true, roomsOnly: true);
			CreateSchoolTexture("GraniteCeiling.png", ContentUtilities.AllFloorsExcept(Floors.F1), SchoolTextType.Ceiling);
			CreateSchoolTexture("woodFloor.png", ContentUtilities.AllFloors, SchoolTextType.Floor);
		}

		private void CreateSchoolTexture(string textureName, Floors[] floors, SchoolTextType[] types, bool existOnClassrooms = false, bool existOnFaculties = false, bool roomsOnly = false, int weight = 100)
		{
			try
			{
				if (roomsOnly && !existOnClassrooms && !existOnFaculties)
					throw new ArgumentException($"The texture: {textureName} supports only rooms but isn\'t applied for any room type");

				if (types.Length == 0)
					throw new ArgumentException("No school texture type has been selected (empty array)");


				var text = AssetManager.TextureFromFile(Path.Combine(modPath, "Textures", "schooltext", textureName));

				CheckForParameters(weight, text, 128, 128, floors);

				textPair.Add(floors);
				textPairForRooms.Add(new bool[]
				{
					existOnClassrooms,
					existOnFaculties,
					roomsOnly
				});
				allTexts.Add(new WeightedTexture2D
				{
					selection = text,
					weight = weight
				}, types);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Debug.LogWarning($"The texture: {textureName} was failed to be loaded, the file name inputted is probably wrong");
				Debug.LogWarning("Your texture won\'t be added to the level");
			}

		}

		private void CreateSchoolTexture(string textureName, Floors[] floors, SchoolTextType type, bool existOnClassrooms = false, bool existOnFaculties = false, bool roomsOnly = false, int weight = 100) => CreateSchoolTexture(textureName, floors, new SchoolTextType[] { type }, existOnClassrooms, existOnFaculties, roomsOnly, weight);


		// --------- Object Builders Modifiers ----------
		// Basically modifies/add builders for the game, example are builders for lockers and swinging doors for example

		// CreateStandardHallBuilder > Simplest method, because of how the hallbuilders work, they are used in swinging door and locker gen, they basically receive a random hall tile from the generator and the builder has to work around that tile
		// name: name of the hallbuilder
		// weight: chance to be used (it's a percentage chance, which means it goes from 0 - 100, higher the value is higher the chances of being chosen)

		// CreateAndAddObjBuilder > Simple method aswell, but this one has a lot more accessibility since you can actually use parameters provided by the generator to modify the level itself (still only hall tiles are provided)+
		// name: name of the obj builder
		// weight (optional, if not provided, it'll be assigned as a forcedObjectBuilder): chance of being chosen
		// Floors: what floors does it appear

		// Create Room Builder > Simple method that creates a builder for a specific room (can be customizable one or an existent one)
		// name: name of builder 
		// weight: chance to the builder be chosen
		// categories: what rooms does the builder support
		// Floors (optional, all by default): Set which floors will the custom room spawn

		// -------From below, only parameters for creation of custom rooms---------

		// roomName: IF your builder is for a custom room, put the name of the room here
		// onlyConnectedToHall: if the custom room can only be connected to a hallway
		// ceiling: textures for the ceiling (use CreateRawSchoolTexture() method, uses png from Textures/schooltext)
		// wall: textures for the wall (use CreateRawSchoolTexture() method, uses png from Textures/schooltext)
		// floor: textures for the floor (use CreateRawSchoolTexture() method, uses png from Textures/schooltext)
		// doorOpenPath: file name of the texture of the door opened (must be png, and should be inside Textures/customRooms folder)
		// doorClosedPath: file name of the texture of the door closed (must be png, and should be inside Textures/customRooms folder)
		// mapColor: color of the room on the map
		// minAmount (optional): minimal amount of rooms that can spawn (applies for every floor, not by just one)
		// maxAmount (optional): maximal amount of rooms that can spawn (applies for every floor, not by just one)
		// darkRoom (optional, disabled by default): if you want the room to be naturally dark (in order to create special lighting for example), you can always set this true
		// You can use AddCustomBuilderToExistentCustomRoom() to do what the method name literally says, add a builder to a custom room, all you gotta put is just the name of the builder and the roomName that was used (MUST BE AFTER CREATING THE CUSTOM ROOM, doesn't support builders for existent rooms)

		public void SetupObjectBuilders()
		{
			executedRooms.Clear(); // Clears up an important list before executing
			if (addedBuilders)
				return;
			addedBuilders = true;

			// Extra functions for builders here (Refer the methods from ExtraStuff/Builders.cs)
			// Current supported categories: Office, Class, Faculty
			// Any other category besides these 3 won't work (simply won't run because there is no code to apply them)
			// These functions applies for every floor

			// Additions for RoomBuilders Here

			allExtraStuffForRoomBuilders.Add(ReplacementBuilders.CreateWallClocks, ContentUtilities.Array(RoomCategory.Class, RoomCategory.Office));
			allExtraStuffForRoomBuilders.Add(ReplacementBuilders.CreateLightBulbs, ContentUtilities.Array(RoomCategory.Faculty));

			// New Object Builders Here

			CreateAndAddObjBuilder<WallBellBuilder>("Bell Builder", ContentUtilities.AllFloors);
			CreateAndAddObjBuilder<VentBuilder>("Vent Builder", 85, ContentUtilities.AllFloorsExcept(Floors.F1));

			// New Room Builders Here

			CreateRoomBuilder<LossyClassBuilder>("Messy Class Builder", 60, ContentUtilities.Array(RoomCategory.Class)); // PixelGuy

			 CreateRoomBuilder<BathBuilder>("BathroomBuilder", 50, "bathroom", ContentUtilities.Array(Floors.F1), false, ContentUtilities.Array(CreateRawSchoolTexture("bathroomCeiling.png")),
				ContentUtilities.Array(CreateRawSchoolTexture("bathroomWall.png")),
				ContentUtilities.Array(CreateRawSchoolTexture("bathroomFloor.png")), "bathDoorOpened.png", "bathDoorClosed.png", Color.white, 
				ContentUtilities.Array(ContentUtilities.LightPrefab, CreateExtraDecoration_Raw("long_hanginglamp.png", 200, 30, ContentUtilities.AllCategories, true, true, Vector3.up * 8.2f)), 0, 1); // PixelGuy >> Bathroom for F1
			DuplicateRoomBuilder("bathroom", ContentUtilities.Array(Floors.F2, Floors.END), 1, 2); //PixelGuy > Bathroom for F2 & END
			DuplicateRoomBuilder("bathroom", Floors.F3, 2, 4); // PixelGuy >> Bathroom for F3

			CreateRoomBuilder<AbandonedBuilder>("AbandonedRoomBuilder", 250 /*CHANGE WHEN FINISHED*/, "abandoned", ContentUtilities.Array(Floors.END), false, ContentUtilities.Array(CreateRawSchoolTexture("GraniteCeiling.png")),
			ContentUtilities.Array(CreateRawSchoolTexture("moldWall.png")),
			ContentUtilities.Array(CreateRawSchoolTexture("woodFloor.png")), "oldDoorOpen.png", "oldDoorClosed.png", Color.white, 
			ContentUtilities.Array(ContentUtilities.LightPrefab, CreateExtraDecoration_Raw("long_hanginglamp.png", 200, 30, ContentUtilities.AllCategories, true, true, Vector3.up * 8.2f)), 0, 1, true); // JDvideosPR >> Abandoned locked room for F3

			// Note: if you want to create custom lights for the room, you can always use CreateExtraDecoration_Raw, and if you want to keep the original, you include ContentUtilities.LightPrefab on the array

			//CreateRoomBuilder<LossyClassBuilder>("Messy Class Builder", 60, "thisRoom", ContentUtilities.AllFloors, false, ContentUtilities.Array(CreateRawSchoolTexture("lightCarpet.png")), ContentUtilities.Array(CreateRawSchoolTexture("lightCarpet.png")), ContentUtilities.Array(CreateRawSchoolTexture("lightCarpet.png")), "placeholderOpen.png", "placeholderClosed.png", Color.red, 3, 5, true);
			//AddCustomBuilderToExistentCustomRoom<LossyClassBuilder>("Another Messy Class Builder", 10, "thisRoom"); Use these 2 code lines as example for a custom room

			// Pro Tip: You can stack multiple custom room builders like the bathroom one, and change the amount of rooms for each floor! Just use DuplicateRoomBuilder(name, floors, min and max amount) << parameters, please put the name of the room builder as the bathroom does



		}

		private void DuplicateRoomBuilder(string name, Floors[] floors, int minAmount, int maxAmount)
		{
			try
			{
				CheckForParameters(floors);
				if (!customRoomEnums.GetRoomByName(name, out RoomCategory cat))
					throw new ArgumentException($"Invalid Room Name: \"{name}\"");
				
				var newData = roomDatas[roomDatas.IndexAt(x => x.IsANewRoom && x.Rooms.Contains(cat))];
				newData.SetParameters(minAmount, maxAmount, floors);
				roomDatas.Add(newData);
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Debug.Log("Failed to duplicate the room builder: " + name);
			}
		}
		private void DuplicateRoomBuilder(string name, Floors floor, int minAmount, int maxAmount) => DuplicateRoomBuilder(name, ContentUtilities.Array(floor), minAmount, maxAmount);

		private void CreateRoomBuilder<B>(string name, int weight, RoomCategory[] categories, Floors[] floors, out bool success, out WeightedRoomBuilder builder) where B : RoomBuilder // Create room builder
		{
			var obj = new GameObject("CustomRoomBuilder_" + name);
			try
			{
				CheckForParameters(weight, floors);

				DontDestroyOnLoad(obj);
				var comp = obj.AddComponent<B>();

				var bld = new WeightedRoomBuilder()
				{
					selection = comp,
					weight = weight
				};
				builder = bld;

				roomDatas.Add(RoomData.CreateBuilder(floors, bld, categories));

				success = true;
			}
			catch (Exception e)
			{
				success = false;
				builder = null;
				Debug.LogException(e);
				Debug.LogWarning("Failed to create the Custom Room Builder: " + name);
				Destroy(obj);
			}
		}
		// Bunch of methods that extend or simplify the main one
		private void CreateRoomBuilder<B>(string name, int weight, RoomCategory[] categories, Floors[] floors) where B : RoomBuilder => CreateRoomBuilder<B>(name, weight, categories, floors, out _, out _);

		private void CreateRoomBuilder<B>(string name, int weight, RoomCategory[] categories) where B : RoomBuilder => CreateRoomBuilder<B>(name, weight, categories, ContentUtilities.AllFloors);

		private void CreateRoomBuilder<B>(string name, int weight, string roomName, Floors[] floors, bool onlyConnectedToHall, WeightedTexture2D_ForRooms[] ceiling, WeightedTexture2D_ForRooms[] wall, WeightedTexture2D_ForRooms[] floor, string doorOpenPath, string doorClosedPath, Color mapColor, int minAmount = 1, int maxAmount = 1, bool darkRoom = false) where B : RoomBuilder =>
		CreateRoomBuilder<B>(name, weight, roomName, floors, onlyConnectedToHall, ceiling, wall, floor, doorOpenPath, doorClosedPath, mapColor, ContentUtilities.Array(ContentUtilities.LightPrefab), minAmount, maxAmount, darkRoom);

		private void AddCustomBuilderToExistentCustomRoom<B>(string name, int weight, string roomName) where B : RoomBuilder 
		{
			var cat = customRoomEnums.GetRoomByName(roomName);
			CreateRoomBuilder<B>(name, weight, ContentUtilities.Array(RoomCategory.Null), ContentUtilities.AllFloors, out _, out WeightedRoomBuilder builder);
			roomDatas.RemoveAt(roomDatas.Count - 1);
			roomDatas[roomDatas.IndexAt(x => x.IsANewRoom && x.Rooms.Contains(cat))].AddBuilder(builder);
		}

		private void CreateRoomBuilder<B>(string name, int weight, string roomName, Floors[] floors, bool onlyConnectedToHall, WeightedTexture2D_ForRooms[] ceiling, WeightedTexture2D_ForRooms[] wall, WeightedTexture2D_ForRooms[] floor, string doorOpenPath, string doorClosedPath, Color mapColor, WeightedTransform[] lightPre, int minAmount = 1, int maxAmount = 1, bool darkRoom = false) where B : RoomBuilder // Creation of custom rooms below
		{
			if (!File.Exists(Path.Combine(modPath, "Textures", "customRooms", doorOpenPath)) || !File.Exists(Path.Combine(modPath, "Textures", "customRooms", doorClosedPath)))
			{
				Debug.LogException(new FileNotFoundException("Door Textures for Room: " + roomName + $"doesn\'t exist ({doorOpenPath} || {doorClosedPath})"));
				return;
			}
			RoomCategory rEnum = EnumExtensions.ExtendEnum<RoomCategory>(roomName);


			CreateRoomBuilder<B>(name, weight, ContentUtilities.Array(rEnum), floors, out bool success, out WeightedRoomBuilder builder);

			if (maxAmount < minAmount) maxAmount = minAmount + 1;

			if (!success) return;

			ceiling.Do(x => x.SetupVariables(SchoolTextType.Ceiling, rEnum));
			wall.Do(x => x.SetupVariables(SchoolTextType.Wall, rEnum));
			floor.Do(x => x.SetupVariables(SchoolTextType.Floor, rEnum));
				customRoomEnums.Add(rEnum);
			foreach (var light in lightPre)
			{
				light.selection.name += "_HangingLight";
			}

			roomDatas[roomDatas.Count - 1] = RoomData.ConvertToRoom(roomDatas[roomDatas.Count - 1], onlyConnectedToHall, minAmount, maxAmount, Path.Combine(modPath, "Textures", "customRooms", doorClosedPath), Path.Combine(modPath, "Textures", "customRooms", doorOpenPath), mapColor, darkRoom, rEnum, ceiling, wall, floor, lightPre);
			
		}

		private WeightedTexture2D_ForRooms CreateRawSchoolTexture(string textureName, int weight = 100)
		{
			try
			{
				var text = AssetManager.TextureFromFile(Path.Combine(modPath, "Textures", "schooltext", textureName));

				CheckForParameters(weight, text, 128, 128);

				var text2 = WeightedTexture2D_ForRooms.Create(text, weight, SchoolTextType.None);

				return text2;
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Debug.LogWarning($"The texture: {textureName} was failed to be loaded into the custom room builder, the file name inputted is probably wrong");
				Debug.LogWarning("Your texture won\'t be added to the level");
				return new WeightedTexture2D_ForRooms(null, SchoolTextType.None);
			}

		}

		private void CreateStandardHallBuilder<B>(string name, float weight) where B : HallBuilder
		{
			var obj = new GameObject("CustomHallBuilder_" + name);
			try
			{
				CheckForParameters((int)weight, 100);

				DontDestroyOnLoad(obj);
				var comp = obj.AddComponent<B>();

				builders.Add(new BuilderHolder(comp, weight));
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Debug.LogWarning("Failed to create the StandardHallBuilder: " + name);
				Debug.LogWarning("Must happen due to an invalid weight value");
				Destroy(obj);
			}
		}

		private WeightedObjectBuilder CreateObjectBuilder<B>(string name, int weight, Floors[] floors) where B : ObjectBuilder => new WeightedObjectBuilder() { selection = CreateWeightedObjectBuilder<B>(name, weight, floors), weight = weight };

		private ObjectBuilder CreateWeightedObjectBuilder<B>(string name, int weight, Floors[] floors) where B : ObjectBuilder // Weighted Object Builder
		{
			var obj = new GameObject("CustomObjectBuilder_" + name);
			try
			{
				CheckForParameters(weight, floors);


				DontDestroyOnLoad(obj);
				var comp = obj.AddComponent<B>();
				comp.obstacle = EnumExtensions.ExtendEnum<Obstacle>(name);
				customObstacleEnums.Add(comp.obstacle);

				return comp;
			}

			catch (Exception e)
			{
				Debug.LogException(e);
				Debug.LogWarning("Failed to create obj builder, must happen due to invalid weight value");

				Destroy(obj);
				return null;
			}
		}

		private void CreateAndAddObjBuilder<B>(string name, int weight, Floors[] floors) where B : ObjectBuilder // Random
		{
			var obj = CreateObjectBuilder<B>(name, weight, floors);
			if (obj.selection)
				builders.Add(new BuilderHolder(obj.selection, weight, floors));

		}

		private void CreateAndAddObjBuilder<B>(string name, Floors[] floors) where B : ObjectBuilder // Non Random
		{
			var obj = CreateObjectBuilder<B>(name, 1, floors);
			if (obj.selection)
				builders.Add(new BuilderHolder(obj.selection, floors));

		}

		// -- Extras --
		// Down on this section, it'll contain some methods for creation of extra stuff to the game
		// MakePrincipalScoldingAudio()
		// scoldName: scolding name that principal will look for to play when scolding player
		// audioName: the name of the audio file (must be .wav in Audios/npc)
		// audKey: key for the audio caption
		// CreateExtraDecoration()
		// texturePath: file name of the decoration texture (in Textures folder, must be .png)
		// weight: chance to spawn
		// pixelsPerUnit: size of image, lower value means a bigger image
		// categories: what rooms should spawn this (you can also include custom room categories with customRoomEnums.GetRoomByName())
		// Independent (disabled by default): if you want to use this decoration to something else instead of being a decoration on tables, set this to independent (setting this up makes it actually independent and won't spawn naturally)
		// SeparatedSprite (disabled by default): if the decoration will be have a separated sprite from it's gameobject, basically making the sprite child of the main gameobject (Necessary if the decoration is going to have a collider)
		// SpriteOffset (only works with separatedsprite on): changes the sprite's position without affecting the main gameobject (parent)
		public void SetupExtraContent()
		{
			if (!addedExtraContent[0]) // Principal Audios Here
			{
				addedExtraContent[0] = true;
				MakePrincipalScoldingAudio("breakproperty", "principal_nopropertybreak.wav", "Vfx_PRI_NoPropertyBreak");
				MakePrincipalScoldingAudio("gumming", "principal_nospittinggums.wav", "Vfx_PRI_NoGumming");
				MakePrincipalScoldingAudio("stabbing", "principal_nostabbing.wav", "Vfx_PRI_NoStabbing");

			}
			if (!addedExtraContent[1]) // Extra Decorations Here
			{
				addedExtraContent[1] = true;
				CreateExtraDecoration("lightBulb.png", 100, 50, ContentUtilities.Array(RoomCategory.Faculty), true);
				CreateExtraDecoration("sink.png", 1, 60, ContentUtilities.Array(customRoomEnums.GetRoomByName("bathroom")), true, true);
			}
		}

		private void MakePrincipalScoldingAudio(string scoldName, string audioName, string audKey)
		{
			try
			{
				principalLines.Add(scoldName, ObjectCreatorHandlers.CreateSoundObject(ContentUtilities.GetAudioClip(Path.Combine(modPath, "Audio", "npc", audioName)), audKey, SoundType.Voice, new Color(0f, 0.1176f, 0.4824f)));
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				Debug.Log("Failed to add principal\'s scolding audio, probably due to wrong filename");
			}
		}

		private void CreateExtraDecoration(string texturePath, int weight, int pixelsPerUnit, RoomCategory[] categories, bool independent = false, bool separatedSprite = false, Vector3 spriteOffset = default) =>
			_ = CreateExtraDecoration(texturePath, weight, pixelsPerUnit, categories, out _, independent, separatedSprite, spriteOffset);

		private bool CreateExtraDecoration(string texturePath, int weight, int pixelsPerUnit, RoomCategory[] categories, out WeightedTransform transform, bool independent = false, bool separatedSprite = false, Vector3 spriteOffset = default)
		{
			var obj = Instantiate(decorationPre.gameObject);
			
			try
			{
				CheckForParameters(weight);
				WeightedTransform trans = null;
				obj.name = !separatedSprite ? "CustomDecoration_" + Path.GetFileNameWithoutExtension(texturePath) : "Sprite";
				if (separatedSprite)
				{
					var parent = new GameObject("CustomDecoration_" + Path.GetFileNameWithoutExtension(texturePath));
					DontDestroyOnLoad(parent);
					obj.transform.SetParent(parent.transform);
					parent.SetActive(false);
					obj.transform.localPosition = spriteOffset;
					trans = new WeightedTransform() { selection = parent.transform, weight = weight };
				}
				else
				{ 
					trans = new WeightedTransform() { selection = obj.transform, weight = weight };
					obj.SetActive(false);
				}

				
				
				obj.GetComponent<SpriteRenderer>().sprite = AssetManager.SpriteFromTexture2D(AssetManager.TextureFromFile(Path.Combine(modPath, "Textures", texturePath)), new Vector2(0.5f, 0.5f), pixelsPerUnit); // Sets sprite
				DontDestroyOnLoad(obj);
				
				decorations.Add(ExtraDecorationData.Create(trans, categories, independent));
				allDecorations.Add(trans.selection);
				transform = trans;
				return true;
			}
			catch (Exception e)
			{
				Destroy(obj);
				Debug.LogException(e);
				Debug.Log("Failed to create the custom decoration (read the exception description)");
				transform = null;
				return false;
			}
		}

		private WeightedTransform CreateExtraDecoration_Raw(string texturePath, int weight, int pixelsPerUnit, RoomCategory[] categories, bool independent = false, bool separatedSprite = false, Vector3 spriteOffset = default)
		{
			if (CreateExtraDecoration(texturePath, weight, pixelsPerUnit, categories, out WeightedTransform transform, independent, separatedSprite, spriteOffset))
			{
				decorations.RemoveAt(decorations.Count - 1);
				return transform;
			}

			return null;
		}


		// ---- Exception Throwers ----

		/// <summary>
		/// Check for the parameters provided and throw the exceptions in case they are wrong.
		/// On this case, it checks if the <paramref name="weight"/> is below or equal to 0
		/// </summary>
		/// <param name="weight"></param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		private void CheckForParameters(int weight)
		{
			if (weight <= 0)
				throw new ArgumentOutOfRangeException("The weight value is below or equal to 0");
		}
		/// <summary>
		/// Check for the parameters provided and throw the exceptions in case they are wrong.
		/// On this case, it checks if the <paramref name="weight"/> is below or equal to 0 or above the <paramref name="max"/> set
		/// </summary>
		/// <param name="weight"></param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>

		private void CheckForParameters(int weight, int max)
		{
			if (weight <= 0 || weight > max)
				throw new ArgumentOutOfRangeException("The weight value is below or equal to 0 or above the " + max);
		}
		/// <summary>
		/// Check for the parameters provided and throw the exceptions in case they are wrong.
		/// On this case, it checks if any of the <paramref name="weights"/> is below or equal to 0
		/// </summary>
		/// <param name="weight"></param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		private void CheckForParameters(params int[] weights)
		{
			if (weights.Any(x => x <= 0))
				throw new ArgumentOutOfRangeException("The weight value is bwlor or equal to 0");
		}

		/// <summary>
		/// Check for the parameters provided and throw the exceptions in case they are wrong.
		/// On this case, it checks if the <paramref name="weight"/> is below or equal to 0, if the <paramref name="floors"/> array is empty or has invalid enum (Floors.None)
		/// </summary>
		/// <param name="weight"></param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <exception cref="ArgumentException"></exception>
		private void CheckForParameters(int weight, Floors[] floors)
		{
			CheckForParameters(weight);

			if (floors.Length == 0)
				throw new ArgumentException("No floors have been added for spawn");

			if (floors.Contains(Floors.None))
				throw new ArgumentException("Floors.None enum has been included on the array, that isn\'t allowed");
		}

		/// <summary>
		/// Check for the parameters provided and throw the exceptions in case they are wrong.
		/// On this case, it checks if the <paramref name="weight"/> is below or equal to 0, if the <paramref name="floors"/> array is empty or has invalid enum (Floors.None)
		/// </summary>
		/// <param name="weight"></param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <exception cref="ArgumentException"></exception>
		private void CheckForParameters(Floors[] floors)
		{
			if (floors.Length == 0)
				throw new ArgumentException("No floors have been added for spawn");

			if (floors.Contains(Floors.None))
				throw new ArgumentException("Floors.None enum has been included on the array, that isn\'t allowed");
		}


		/// <summary>
		/// Check for the parameters provided and throw the exceptions in case they are wrong.
		/// On this case, it checks if the <paramref name="weight"/> is below or equal to 0 or if the <paramref name="text"/> provided is not on the right resolution
		/// </summary>
		/// <param name="weight"></param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		private void CheckForParameters(int weight, Texture2D text, int width, int height)
		{
			CheckForParameters(weight);

			if (text.height != height || text.width != width)
				throw new ArgumentOutOfRangeException($"The poster\'s resolution is invalid (Texture Current Resolution: {text.height}x{text.width} > must be {height}x{width})");
		}
		/// <summary>
		/// Check for the parameters provided and throw the exceptions in case they are wrong.
		/// On this case, it checks if the <paramref name="weight"/> is below or equal to 0, if the <paramref name="text"/> provided is not on the right resolution, if the <paramref name="floors"/> array is empty or has invalid enum (Floors.None)
		/// </summary>
		/// <param name="weight"></param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <exception cref="ArgumentException"></exception>
		private void CheckForParameters(int weight, Texture2D text, int width, int height, Floors[] floors)
		{
			CheckForParameters(weight, floors);

			if (text.height != height || text.width != width)
				throw new ArgumentOutOfRangeException($"The poster\'s resolution is invalid (Texture Current Resolution: {text.height}x{text.width} > must be {height}x{width})");
		}
		/// <summary>
		/// Check for the parameters provided and throw the exceptions in case they are wrong.
		/// On this case, it checks if the <paramref name="weight"/> is below or equal to 0 or if the room category chosen is invalid or unsupported by current operation
		/// </summary>
		/// <param name="weight"></param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// <exception cref="UnsupportedRoomCategoryException"></exception>
		private void CheckForParameters(int weight, RoomCategory[] categories, RoomCategory[] supportedCategories)
		{
			CheckForParameters(weight);

			foreach (var cat in categories)
			{
				if (!supportedCategories.Contains(cat))
					throw new UnsupportedRoomCategoryException(cat);
			}
		}

		/// <summary>
		/// Check for the parameters provided and throw the exceptions in case they are wrong.
		/// On this case, it checks if the <paramref name="weight"/> is below or equal to 0, if the <paramref name="floors"/> array is empty or if the room category chosen is invalid or unsupported by current operation
		/// </summary>
		/// <param name="weight"></param>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		/// /// <exception cref="ArgumentException"></exception>
		/// <exception cref="UnsupportedRoomCategoryException"></exception>
		private void CheckForParameters(int weight, Floors[] floors, RoomCategory[] categories, RoomCategory[] supportedCategories)
		{
			CheckForParameters(weight, floors);

			foreach (var cat in categories)
			{
				if (!supportedCategories.Contains(cat))
					throw new UnsupportedRoomCategoryException(cat);
			}
		}


		public GameObject beans; // Most important game object for npc creation

		public PosterObject posterPre; // First poster instance to be used

		public Transform decorationPre;

		private readonly Dictionary<Floors, bool> accessedNPCs = new Dictionary<Floors, bool>() // Prevents the weights from being added again (npcs)
		{
			{ Floors.F1, false },
			{ Floors.F2, false },
			{ Floors.F3, false },
			{ Floors.END, false }
		};

		private readonly Dictionary<Floors, bool> accessedItems = new Dictionary<Floors, bool>() // Prevents the weights from being added again (items)
		{
			{ Floors.F1, false },
			{ Floors.F2, false },
			{ Floors.F3, false },
			{ Floors.END, false }
		};

		private readonly Dictionary<Floors, bool> accessedShopItems = new Dictionary<Floors, bool>() // Prevents the weights from being added again (items)
		{
			{ Floors.F1, false },
			{ Floors.F2, false },
			{ Floors.F3, false },
			{ Floors.END, false }
		};

		private readonly Dictionary<Floors, bool> accessedEvents = new Dictionary<Floors, bool>() // Prevents the weights from being added again (items)
		{
			{ Floors.F1, false },
			{ Floors.F2, false },
			{ Floors.F3, false },
			{ Floors.END, false }
		};

		private readonly Dictionary<Floors, bool> accessedExtraStuff = new Dictionary<Floors, bool>()
		{
			{ Floors.F1, false },
			{ Floors.F2, false },
			{ Floors.F3, false },
			{ Floors.END, false }
		};

		private readonly List<WeightedNPC> allNpcs = new List<WeightedNPC>();

		private readonly List<WeightedItemObject> allNewItems = new List<WeightedItemObject>();

		private readonly Dictionary<WeightedItemObject, Floors[]> allShoppingItems = new Dictionary<WeightedItemObject, Floors[]>();

		private readonly List<WeightedRandomEvent> allEvents = new List<WeightedRandomEvent>();

		private readonly List<WeightedPosterObject> allPosters = new List<WeightedPosterObject>();

		private readonly List<WeightedPosterObject> allChalkboards = new List<WeightedPosterObject>();

		// Builders Classes

		public class BuilderHolder
		{
			public BuilderHolder(HallBuilder builder, float weight)
			{
				Type = BuilderType.RandomHallBuilder;
				HallBuilder = new RandomHallBuilder() { chance = weight, selectable = builder };
				AvailableFloors = ContentUtilities.AllFloors;
			}
			public BuilderHolder(ObjectBuilder builder, Floors[] floors)
			{
				Type = BuilderType.ForcedObjectBuilder;
				ForcedObjectBuilder = builder;
				AvailableFloors = floors;
			}
			public BuilderHolder(ObjectBuilder builder, int weight, Floors[] floors)
			{
				Type = BuilderType.RandomObjectBuilder;
				RandomObjectBuilder = new WeightedObjectBuilder() { weight = weight, selection = builder };
				AvailableFloors = floors;
			}
			public BuilderType Type { get; }
			public Floors[] AvailableFloors { get; }

			public RandomHallBuilder HallBuilder { get; }

			public ObjectBuilder ForcedObjectBuilder { get; }

			public WeightedObjectBuilder RandomObjectBuilder { get; }
		}

		private readonly List<BuilderHolder> builders = new List<BuilderHolder>();

		private readonly Dictionary<ExtraBuilder, RoomCategory[]> allExtraStuffForRoomBuilders = new Dictionary<ExtraBuilder, RoomCategory[]>(); // functions that will run after the methods of the builders (very cool, huh?)

		// End of builders

		private readonly Dictionary<WeightedTexture2D, SchoolTextType[]> allTexts = new Dictionary<WeightedTexture2D, SchoolTextType[]>();

		private readonly List<Floors[]> npcPair = new List<Floors[]>();

		private readonly List<Floors[]> itemPair = new List<Floors[]>();

		private readonly List<Floors[]> eventPair = new List<Floors[]>();

		private readonly List<Floors[]> textPair = new List<Floors[]>();

		private readonly List<bool[]> textPairForRooms = new List<bool[]>();

		public struct RoomData
		{
			public RoomData(bool onlyHalls, int minAmount, int maxAmount, Floors[] floors, string doorClosedPath, string doorOpenedPath, Color mapColor, bool isRoomDark, bool isANewRoom, WeightedRoomBuilder[] builders, RoomCategory room, WeightedTexture2D_ForRooms[] ceilings, WeightedTexture2D_ForRooms[] walls, WeightedTexture2D_ForRooms[] tfloors, WeightedTransform[] lightPre) // New Room Creation
			{
				OnlyHalls = onlyHalls;
				MinAmount = minAmount;
				MaxAmount = maxAmount;
				AvailableFloors = floors;

				var newMat = ScriptableObject.CreateInstance<StandardDoorMats>();
				newMat.open = new Material(EnvironmentExtraVariables.lb.ld.classDoorMat.open) { mainTexture = AssetManager.TextureFromFile(doorOpenedPath) };
				newMat.shut = new Material(EnvironmentExtraVariables.lb.ld.classDoorMat.shut) { mainTexture = AssetManager.TextureFromFile(doorClosedPath) };
				newMat.name = $"{builders[0].selection.name}Door_Mat";
				DoorTextures = newMat;

				MapColor = mapColor;
				IsDarkRoom = isRoomDark;
				IsANewRoom = isANewRoom;
				Builders = builders;
				Rooms = ContentUtilities.Array(room);
				Textures = new List<WeightedTexture2D_ForRooms[]>
				{
					ceilings,
					walls,
					tfloors
				};
				LightPre = lightPre;
				Decorations = Array.Empty<WeightedTransform>();
			}

			public RoomData(bool onlyHalls, int minAmount, int maxAmount, Floors[] floors, StandardDoorMats door, Color mapColor, bool isRoomDark, bool isANewRoom, WeightedRoomBuilder[] builders, RoomCategory room, WeightedTexture2D_ForRooms[] ceilings, WeightedTexture2D_ForRooms[] walls, WeightedTexture2D_ForRooms[] tfloors, WeightedTransform[] lightPre)
			{
				OnlyHalls = onlyHalls;
				MinAmount = minAmount;
				MaxAmount = maxAmount;
				AvailableFloors = floors;
				DoorTextures = door;
				MapColor = mapColor;
				IsDarkRoom = isRoomDark;
				IsANewRoom = isANewRoom;
				Builders = builders;
				Rooms = ContentUtilities.Array(room);
				Textures = new List<WeightedTexture2D_ForRooms[]>
				{
					ceilings,
					walls,
					tfloors
				};
				LightPre = lightPre;
				Decorations = Array.Empty<WeightedTransform>();
			}

			public RoomData(Floors[] floors, WeightedRoomBuilder bld, RoomCategory[] rooms) // Just a builder
			{
				AvailableFloors = floors;
				Builders = ContentUtilities.Array(bld);
				Rooms = rooms;
				IsANewRoom = false;
				IsDarkRoom = false;

				// Implementations
				OnlyHalls = false;
				MaxAmount = 0;
				MinAmount = 0;
				DoorTextures = null;
				MapColor = Color.white;
				Textures = new List<WeightedTexture2D_ForRooms[]>();
				LightPre = Array.Empty<WeightedTransform>();
				Decorations = Array.Empty<WeightedTransform>();
			}

			public static RoomData CreateRoom(bool onlyHalls, int minAmount, int maxAmount, Floors[] floors, StandardDoorMats door, Color mapColor, bool isRoomDark, bool isANewRoom, WeightedRoomBuilder[] builders, RoomCategory room, WeightedTexture2D_ForRooms[] ceilings, WeightedTexture2D_ForRooms[] walls, WeightedTexture2D_ForRooms[] tfloors, WeightedTransform[] lightPre) => new RoomData(onlyHalls, minAmount, maxAmount, floors, door, mapColor, isRoomDark, isANewRoom, builders, room, ceilings, walls, tfloors, lightPre);

			public static RoomData CreateRoom(bool onlyHalls, int minAmount, int maxAmount, Floors[] floors, string doorClosedPath, string doorOpenedPath, Color mapColor, bool isRoomDark, bool isANewRoom, WeightedRoomBuilder[] builders, RoomCategory room, WeightedTexture2D_ForRooms[] ceilings, WeightedTexture2D_ForRooms[] walls, WeightedTexture2D_ForRooms[] tfloors, WeightedTransform[] lightPre) => new RoomData(onlyHalls, minAmount, maxAmount, floors, doorClosedPath, doorOpenedPath, mapColor, isRoomDark, isANewRoom, builders, room, ceilings, walls, tfloors, lightPre);

			public static RoomData CreateBuilder(Floors[] floors, WeightedRoomBuilder builder, RoomCategory[] rooms) => new RoomData(floors, builder, rooms);

			public static RoomData CreateBuilder(Floors[] floors, WeightedRoomBuilder builder, RoomCategory room) => new RoomData(floors, builder, ContentUtilities.Array(room));

			public static RoomData ConvertToRoom(RoomData room, bool onlyHalls, int minAmount, int maxAmount, string doorClosedPath, string doorOpenedPath, Color mapColor, bool isRoomDark, RoomCategory roomCategory, WeightedTexture2D_ForRooms[] ceilings, WeightedTexture2D_ForRooms[] walls, WeightedTexture2D_ForRooms[] tfloors, WeightedTransform[] lightPre) => new RoomData(onlyHalls, minAmount, maxAmount, room.AvailableFloors, doorClosedPath, doorOpenedPath, mapColor, isRoomDark, true, room.Builders, roomCategory, ceilings, walls, tfloors, lightPre);

			public void AddBuilder(WeightedRoomBuilder builder) => Builders = Builders.AddToArray(builder);

			public void SetParameters(int minAmount, int maxAmount, Floors[] floors)
			{
				MaxAmount = maxAmount;
				MinAmount = minAmount;
				AvailableFloors = floors;
			}

			public bool OnlyHalls { get; }
			public int MaxAmount { get; private set; }
			public int MinAmount { get; private set; }
			public Floors[] AvailableFloors { get; private set; }
			public StandardDoorMats DoorTextures { get; }
			public Color MapColor { get; }
			public bool IsDarkRoom { get; }
			public bool IsANewRoom { get; }
			public WeightedRoomBuilder[] Builders { get; private set; }
			public RoomCategory[] Rooms { get; }
			public List<WeightedTexture2D_ForRooms[]> Textures { get; }
			public WeightedTransform[] LightPre { get; }
			public WeightedTransform[] Decorations { get; }
		}
		private readonly List<RoomData> roomDatas = new List<RoomData>();

		public class ExtraDecorationData
		{
			public ExtraDecorationData(WeightedTransform prefab, RoomCategory[] rooms, bool independent)
			{
				Prefab = prefab;
				Rooms = rooms;
				IndependentDecoration = independent;
			}

			public static ExtraDecorationData Create(WeightedTransform prefab, RoomCategory[] rooms, bool independent = false) => new ExtraDecorationData(prefab, rooms, independent);

			public WeightedTransform Prefab { get; }

			public RoomCategory[] Rooms { get; }

			public bool IndependentDecoration { get; }
		}

		private readonly List<ExtraDecorationData> decorations = new List<ExtraDecorationData>();

		private readonly List<Transform> allDecorations = new List<Transform>();

		public WeightedTransform[] GetDecorations(RoomCategory category, bool independent = false) => decorations.Where(x => x.Rooms.Contains(category) && x.IndependentDecoration == independent).Select(x => x.Prefab).ToArray();

		public Transform[] GetDecorationTransforms(RoomCategory category, bool independent = false) => decorations.Where(x => x.Rooms.Contains(category) && x.IndependentDecoration == independent).Select(x => x.Prefab.selection).ToArray();

		public bool TryGetDecorationTransform(RoomCategory category, bool independent, string name, out Transform obj)
		{
			try
			{
				obj = decorations.Where(x => x.Rooms.Contains(category) && x.IndependentDecoration == independent).Select(x => x.Prefab.selection).First(x => x.name.ToLower().Contains(name.ToLower()));
				return true;
			}
			catch
			{
				obj = null;
				return false;
			}
		}

		public void TurnDecorations(bool turn) => allDecorations.ForEach(x => x.gameObject.SetActive(turn));

		public RoomData GetRoom(RoomCategory room) => roomDatas.First(x => x.Rooms.Contains(room));

		public bool TryGetRoom(RoomCategory room, out RoomData roomDat)
		{
			try
			{
				roomDat = roomDatas.First(x => x.Rooms.Contains(room));
				return true;
			}
			catch
			{
				roomDat = new RoomData();
				return false;
			}
		}

		public RoomData[] GetRooms(RoomCategory room) => roomDatas.Where(x => x.Rooms.Contains(room)).ToArray();

		public RoomData[] GetRooms(Floors floor) => roomDatas.Where(x => x.AvailableFloors.Contains(floor)).ToArray();

		public bool HasRoom(RoomCategory room) => roomDatas.Any(x => x.Rooms.Contains(room));

		public bool HasRoom(string room)
		{
			var target = customRoomEnums.GetRoomByName(room);
			return roomDatas.Any(x => x.Rooms.Contains(target));
		}

		public List<WeightedNPC> GetNPCs(Floors floor, bool onlyReplacementNPCs = false)
		{
			if (accessedNPCs[floor] && !onlyReplacementNPCs)
				return new List<WeightedNPC>();

			accessedNPCs[floor] = true;
			var npcs = new List<WeightedNPC>();
			for (int i = 0; i < allNpcs.Count; i++)
			{
				if (npcPair[i].Contains(floor) && ((!onlyReplacementNPCs && allNpcs[i].selection.gameObject.GetComponent<CustomNPCData>().replacementCharacters.Length == 0) || (allNpcs[i].selection.gameObject.GetComponent<CustomNPCData>().replacementCharacters.Length > 0 && onlyReplacementNPCs))) // Check whether the npc is from said floor and if it is a replacement NPC or not
				{
					npcs.Add(allNpcs[i]);
				}
			}
			return npcs;
		}

		public List<WeightedRandomEvent> GetEvents(Floors floor)
		{
			if (accessedEvents[floor])
				return new List<WeightedRandomEvent>();

			accessedEvents[floor] = true;
			var npcs = new List<WeightedRandomEvent>();
			for (int i = 0; i < allEvents.Count; i++)
			{
				if (eventPair[i].Contains(floor)) // Check whether the item is from said floor
				{
					npcs.Add(allEvents[i]);
				}
			}
			return npcs;
		}

		public List<WeightedItemObject> GetItems(Floors floor)
		{
			if (accessedItems[floor])
				return new List<WeightedItemObject>();

			accessedItems[floor] = true;
			var npcs = new List<WeightedItemObject>();
			for (int i = 0; i < allNewItems.Count; i++)
			{
				if (itemPair[i].Contains(floor)) // Check whether the item is from said floor
				{
					npcs.Add(allNewItems[i]);
				}
			}
			return npcs;
		}

		public List<WeightedItemObject> GetShoppingItems(Floors floor)
		{
			if (accessedShopItems[floor])
				return new List<WeightedItemObject>();

			accessedShopItems[floor] = true;
			var npcs = new List<WeightedItemObject>();
			foreach (var item in allShoppingItems)
			{
				if (item.Value.Contains(floor)) // Check whether the item is from said floor
				{
					npcs.Add(item.Key);
				}
			}
			return npcs;
		}

		public List<WeightedTexture2D> GetSchoolText(Floors floor, SchoolTextType type, int roomType) // 0 = classroom, 1 = faculty room, 2 = if it is rooms only (not useful on this method)
		{
			if (accessedExtraStuff[floor])
				return new List<WeightedTexture2D>();

			var texts = new List<WeightedTexture2D>();
			for (int i = 0; i < allTexts.Count; i++)
			{
				if (textPair[i].Contains(floor) && allTexts.ElementAt(i).Value.Contains(type) && textPairForRooms[i][roomType]) // Check whether the text is from said floor and the texture type
				{
					texts.Add(allTexts.ElementAt(i).Key);
				}
			}
			return texts;

		}

		public List<WeightedTexture2D> GetSchoolText(Floors floor, SchoolTextType type)
		{
			if (accessedExtraStuff[floor])
				return new List<WeightedTexture2D>();

			var texts = new List<WeightedTexture2D>();
			for (int i = 0; i < allTexts.Count; i++)
			{
				if (textPair[i].Contains(floor) && allTexts.ElementAt(i).Value.Contains(type) && !textPairForRooms[i][2]) // Check whether the text is from said floor and the texture type
				{
					texts.Add(allTexts.ElementAt(i).Key);
				}
			}
			return texts;

		}

		public List<RandomHallBuilder> StandardHallBuilders { get => GetBuilderHolders(BuilderType.RandomHallBuilder).Select(x => x.HallBuilder).ToList(); }

		private IEnumerable<BuilderHolder> GetBuilderHolders(BuilderType type) => builders.Where(x => x.Type == type);

		public List<WeightedRoomBuilder> GetNewRoomBuilders(RoomCategory neededCategory)
		{
			List<WeightedRoomBuilder> bdrs = new List<WeightedRoomBuilder>();
			foreach (var bld in roomDatas)
			{
				if (!bld.IsANewRoom && bld.Rooms.Contains(neededCategory))
				{
					bdrs.AddRange(bld.Builders);
				}
			}
			return bdrs;
		}

		public List<ObjectBuilder> GetForcedHallBuilders(Floors floor)
		{
			List<ObjectBuilder> builders = new List<ObjectBuilder>();
			foreach (var bld in GetBuilderHolders(BuilderType.ForcedObjectBuilder))
			{
				if (bld.AvailableFloors.Contains(floor))
				{
					builders.Add(bld.ForcedObjectBuilder);
				}
			}
			return builders;
		}

		public List<WeightedObjectBuilder> GetObjectBuilders(Floors floor)
		{
			List<WeightedObjectBuilder> builders = new List<WeightedObjectBuilder>();
			foreach (var bld in GetBuilderHolders(BuilderType.RandomObjectBuilder))
			{
				if (bld.AvailableFloors.Contains(floor))
				{
					builders.Add(bld.RandomObjectBuilder);
				}
			}
			return builders;
		}

		public IEnumerator ExecuteRoomBuilderFunctions(LevelBuilder lb, RoomController controller, System.Random rng, RoomBuilder bld)
		{
			if (executedRooms.Contains(controller)) yield break; // Makes sure any additional builder inside the main builders (with same room category) doesn't make the custom bldrs run twice
			executedRooms.Add(controller);
			while (!lb.doorsFinished || bld.Building) { yield return null; }

			if (currentValidCategories.Contains(controller.category))
			{
				var stuff = allExtraStuffForRoomBuilders.Where(x => x.Value.Contains(controller.category));
				if (stuff.Count() > 0)
				{
					var buildVar = AccessTools.Field(typeof(RoomBuilder), "building");
					buildVar.SetValue(bld, true);
					stuff.Do(x => x.Key(lb, controller, rng));
					buildVar.SetValue(bld, false);
				}
			}

			yield break;
		}

		private readonly List<RoomController> executedRooms = new List<RoomController>();

		public void AssignCustomRooms() // main method to assign custom rooms
		{
			var currentFloor = EnvironmentExtraVariables.currentFloor;
			for (int i = 0; i < roomDatas.Count; i++)
			{
				var roomDat = roomDatas[i];
				if (!roomDat.IsANewRoom) continue;


				var potentialRooms = GetPotentialRooms(roomDat.OnlyHalls);

				if (potentialRooms.Count == 0) continue;

				int amount = selectedAmounts[i];
				for (int x = 0; x < amount; x++)
				{
					if (potentialRooms.Count == 0) break;

					int num28 = EnvironmentExtraVariables.lb.controlledRNG.Next(potentialRooms.Count);
					var room = potentialRooms[num28];
					potentialRooms.RemoveAt(num28);
					room.category = roomDat.Rooms[0];


					room.doorMats = roomDat.DoorTextures;

					TextureArea(room, WeightedTexture2D.ControlledRandomSelection(roomDat.Textures[0].Select(z => z.selection).ToArray(), EnvironmentExtraVariables.lb.controlledRNG),
						WeightedTexture2D.ControlledRandomSelection(roomDat.Textures[1].Select(z => z.selection).ToArray(), EnvironmentExtraVariables.lb.controlledRNG),
						WeightedTexture2D.ControlledRandomSelection(roomDat.Textures[2].Select(z => z.selection).ToArray(), EnvironmentExtraVariables.lb.controlledRNG));

					room.lightPre = WeightedSelection<Transform>.ControlledRandomSelection(roomDat.LightPre, EnvironmentExtraVariables.lb.controlledRNG);
					EnvironmentExtraVariables.lb.activeRoomBuilders.Add(EnvironmentExtraVariables.lb.CreateRoomBuilder(room, WeightedSelection<RoomBuilder>.ControlledRandomSelection(roomDat.Builders.ToArray(), EnvironmentExtraVariables.lb.controlledRNG))); // Chooses room builder and add it
				}




			}

		}

		private void TextureArea(RoomController room, params Texture2D[] textures)
		{
			room.ceilingTex = textures[0];
			room.wallTex = textures[1];
			room.floorTex = textures[2];
			room.GenerateTextureAtlas();
		}

		private List<RoomController> GetPotentialRooms(bool connectedToHall)
		{
			var potentialRooms = new List<RoomController>();
			foreach (RoomController roomController in EnvironmentExtraVariables.lb.rooms)
			{
				if ((!connectedToHall || roomController.adjConnectedRooms.Contains(EnvironmentExtraVariables.lb.halls[0])) && roomController.category == RoomCategory.Null)
				{
					potentialRooms.Add(roomController);
				}
			}
			return potentialRooms;
		}

		public bool HasAccessedFloor(Floors floor) => accessedExtraStuff[floor];

		public void LockAccessedFloor(Floors floor) => accessedExtraStuff[floor] = true;

		public List<WeightedNPC> AllNpcs
		{
			get => allNpcs;
		}

		public List<WeightedRandomEvent> AllEvents
		{
			get => allEvents;
		}

		public List<WeightedPosterObject> AllPosters(bool isChalkBoard)
		{
			return isChalkBoard ? allChalkboards : allPosters;
		}

		public List<WeightedItemObject> AllNewItems
		{
			get => allNewItems;
		}

		public ItemObject RandomItem
		{
			get => WeightedItemObject.RandomSelection(allItems.ToArray());
		}

		public List<WeightedItemObject> GlobalItems
		{
			get => allItems;
		}

		public List<WeightedItemObject> MysteryItems
		{
			get => mysteryItems;
		}

		public List<WeightedItemObject> PartyItems
		{
			get => partyItems;
		}

		public List<WeightedItem> FieldTripItems
		{
			get
			{
				if (usedFieldTrip)
					return new List<WeightedItem>();
				usedFieldTrip = true;
				return fieldTripItems;

			}
		}

		public LevelObject[] GetLevelObjectCopy(LevelObject ld)
		{
			LevelObject[] array = Array.Empty<LevelObject>();
			var copyArray = copyOfLevelObjects.Where(x => x).OrderBy(x => x.name).ToArray();

			for (int i = 0; i < copyArray.Length; i++)
			{
				if (copyArray[i].name.Contains(ld.name))
				{  // Found, stop
					break;
				}

				array = array.AddToArray(copyArray[i]);
			}

			return array;
		}

		public void AddLevelObject(LevelObject ld)
		{
			foreach (var item in copyOfLevelObjects)
			{
				if (item && item.name.Contains(ld.name)) return;
			}

			for (int i = 0; i < copyOfLevelObjects.Length; i++)
			{
				if (!copyOfLevelObjects[i])
				{
					copyOfLevelObjects[i] = ld;
					break;
				}
			}

		}





		public static ContentManager instance;

		public static string modPath;

		public List<Items> customItemEnums = new List<Items>();

		public List<Character> customNPCEnums = new List<Character>();

		private readonly List<Character> staticNpcs = new List<Character>() { Character.Chalkles, Character.Bully };

		public bool IsNpcStatic(Character npc) => staticNpcs.Contains(npc);

		public List<RandomEventType> customEventEnums = new List<RandomEventType>();

		public List<Obstacle> customObstacleEnums = new List<Obstacle>();

		public List<RoomCategory> customRoomEnums = new List<RoomCategory>();

		readonly Dictionary<string, SoundObject> principalLines = new Dictionary<string, SoundObject>();

		public SoundObject GetPrincipalLine(string scold) => principalLines.ContainsKey(scold) ? principalLines[scold] : null;

		// -------- DONT TOUCH Variables ---------

		private bool addedNPCs = false;

		private bool addedItems = false;

		private bool addedEvents = false;

		private bool addedPosters = false;

		private bool addedTexts = false;

		private bool addedBuilders = false;

		private bool assetsLoaded = false;

		private readonly bool[] addedExtraContent = ContentUtilities.Array(false, false); // Added principal dialogues, added extra decorations

		private readonly List<WeightedItemObject> mysteryItems = new List<WeightedItemObject>();

		private readonly List<WeightedItemObject> partyItems = new List<WeightedItemObject>();

		private readonly List<WeightedItem> fieldTripItems = new List<WeightedItem>();

		private bool usedFieldTrip = false;

		private readonly List<WeightedItemObject> allItems = new List<WeightedItemObject>();

		public bool DebugMode { get; set; }

		public int RoomCount
		{
			get
			{
				if (!EnvironmentExtraVariables.lb) return 0;
				int extra = EnvironmentExtraVariables.events.Any(x => x.Type == RandomEventType.MysteryRoom) ? 1 : 0;
				if (DebugMode) Debug.Log("Has mystery: " + extra);
				int max = 0;
				selectedAmounts = new int[roomDatas.Count];
				for (int i = 0; i < roomDatas.Count; i++)
				{
					int num = 0;
					if (roomDatas[i].IsANewRoom && roomDatas[i].AvailableFloors.Contains(EnvironmentExtraVariables.currentFloor))
						num = EnvironmentExtraVariables.lb.controlledRNG.Next(roomDatas[i].MinAmount, roomDatas[i].MaxAmount + 1);
					max += num;
					selectedAmounts[i] = num;
				}
				return max + extra;
			}
		}

		int[] selectedAmounts;

		public Texture2D sweepPoster; // This variable is used SPECIFICALLY for a patch that changes gotta sweep parameters, so when switching back, I use this variable

		public Sprite sweepSprite; // Same applies here

		private readonly LevelObject[] copyOfLevelObjects = new LevelObject[3]; // A copy of each level object to replace the current one (so custom characters aren't added to the next level)

		readonly RoomCategory[] currentValidCategories = new RoomCategory[] { RoomCategory.Class, RoomCategory.Office, RoomCategory.Faculty };

	}
}

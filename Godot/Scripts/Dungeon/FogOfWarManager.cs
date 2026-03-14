using System.Collections.Generic;
using Godot;

namespace JunkbotArena
{
	/// <summary>
	/// Room-level fog of war. Hides undiscovered rooms, dims explored rooms,
	/// and fully lights the player's current room + neighbors.
	/// </summary>
	public partial class FogOfWarManager : Node
	{
		private DungeonGenerator _generator;
		private readonly HashSet<Vector2I> _discoveredRooms = new();
		private Vector2I _currentRoom;

		private static readonly Vector2I[] Directions =
		{
			new(0, -1), new(0, 1), new(-1, 0), new(1, 0)
		};

		public void Initialize(DungeonGenerator generator)
		{
			_generator = generator;

			// In autoplay mode, reveal the entire map at full brightness
			if (AutoPlayer.Instance != null)
			{
				foreach (var (pos, controller) in _generator.RoomControllers)
					controller.SetFogState(FogState.Active);
				GD.Print("[FogOfWar] AutoPlay: all rooms set to Active");
				return;
			}

			// Find entrance and discover it + neighbors before first frame
			foreach (var (pos, type) in _generator.RoomGrid)
			{
				if (type == RoomType.Entrance)
				{
					_currentRoom = pos;
					DiscoverRoom(pos);
					DiscoverNeighbors(pos);
				}

				// Boss room is always visible — it's the exit, player needs to know where it is
				if (type == RoomType.Boss)
					DiscoverRoom(pos);
			}

			UpdateAllFogStates();
		}

		public override void _Ready()
		{
			GameEvents.OnRoomEntered += OnRoomEntered;
		}

		public override void _ExitTree()
		{
			GameEvents.OnRoomEntered -= OnRoomEntered;
		}

		private void OnRoomEntered(Node roomNode)
		{
			if (roomNode is not RoomController controller) return;
			if (_generator == null || AutoPlayer.Instance != null) return;

			_currentRoom = controller.GridPosition;
			DiscoverRoom(_currentRoom);
			DiscoverNeighbors(_currentRoom);
			UpdateAllFogStates();
		}

		private void DiscoverRoom(Vector2I pos)
		{
			_discoveredRooms.Add(pos);
		}

		private void DiscoverNeighbors(Vector2I pos)
		{
			foreach (var dir in Directions)
			{
				var neighbor = pos + dir;
				if (_generator.RoomGrid.ContainsKey(neighbor))
					DiscoverRoom(neighbor);
			}
		}

		private void UpdateAllFogStates()
		{
			// Build set of active positions (current room + orthogonal neighbors)
			var activePositions = new HashSet<Vector2I> { _currentRoom };
			foreach (var dir in Directions)
			{
				var neighbor = _currentRoom + dir;
				if (_generator.RoomGrid.ContainsKey(neighbor))
					activePositions.Add(neighbor);
			}

			// Set fog state on each room
			foreach (var (pos, controller) in _generator.RoomControllers)
			{
				if (activePositions.Contains(pos))
					controller.SetFogState(FogState.Active);
				else if (_discoveredRooms.Contains(pos))
					controller.SetFogState(FogState.Explored);
				else
					controller.SetFogState(FogState.Hidden);
			}

			// Notify minimap
			GameEvents.OnFogUpdated?.Invoke(_discoveredRooms);
		}
	}
}

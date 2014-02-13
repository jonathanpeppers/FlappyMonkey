﻿#region File Description
//-----------------------------------------------------------------------------
// FlappyMonkey.iOSGame.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;

#endregion
#region Using Statements
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

#endregion
namespace FlappyMonkey
{
	/// <summary>
	/// Default Project Template
	/// </summary>
	public class Game1 : Game
	{
		#region Fields

		GraphicsDeviceManager graphics;
		SpriteBatch spriteBatch;
		// Represents the player
		Player player;
		// Keyboard states used to determine key presses
		KeyboardState currentKeyboardState;
		KeyboardState previousKeyboardState;
		// Gamepad states used to determine button presses
		GamePadState currentGamePadState;
		GamePadState previousGamePadState;
		TouchCollection previousTouches;
		TouchCollection currentTouches;
		ParallaxingBackground ground;
		ParallaxingBackground buildings;
		ParallaxingBackground bushes;
		ParallaxingBackground clouds1;
		ParallaxingBackground clouds2;
		Texture2D mainBackground, wallTexture, topWallCapTexture, bottomWallCapTexture, playerTexture, groundBottom;
		List<Wall> walls = new List<Wall> ();
		// The rate at which the walls appear
		double wallSpanTime, previousWallSpawnTime;
		// A random number generator
		Random random = new Random ();
		//Number that holds the player score
		int score;
		// The font used to display UI elements
		SpriteFont font;
		bool accelActive;
		int wallHeight;
		Rectangle bottomGroundRect;

		#endregion

		#region Initialization

		public Game1 ()
		{
			graphics = new GraphicsDeviceManager (this) {
				#if __OUYA__
				SupportedOrientations = DisplayOrientation.LandscapeLeft |  DisplayOrientation.LandscapeRight,
				#else 
				SupportedOrientations = DisplayOrientation.Portrait,
				#endif
				IsFullScreen = true,
			};
			
			Content.RootDirectory = "Content";
		}

		/// <summary>
		/// Overridden from the base Game.Initialize. Once the GraphicsDevice is setup,
		/// we'll use the viewport to initialize some values.
		/// </summary>
		protected override void Initialize ()
		{
			wallHeight = Math.Min (GraphicsDevice.Viewport.Height, MaxWallheight);
			bottomGroundRect = new Rectangle (0, wallHeight + 1, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height - MaxWallheight);
			player = new Player ();
			ground = new ParallaxingBackground ();
			buildings = new ParallaxingBackground ();
			bushes = new ParallaxingBackground ();
			clouds1 = new ParallaxingBackground ();
			clouds2 = new ParallaxingBackground ();
			base.Initialize ();
		}

		/// <summary>
		/// Load your graphics content.
		/// </summary>
		protected override void LoadContent ()
		{
			// Create a new SpriteBatch, which can be use to draw textures.
			spriteBatch = new SpriteBatch (graphics.GraphicsDevice);
			
			// TODO: use this.Content to load your game content here eg.
			playerTexture = Content.Load<Texture2D> ("monkey");

			player.Initialize (playerTexture, new Vector2 (graphics.GraphicsDevice.Viewport.Width / 3, graphics.GraphicsDevice.Viewport.Height / 2));
			wallTexture = Content.Load<Texture2D> ("pipe");
			topWallCapTexture = Content.Load<Texture2D> ("pipeTopCap");
			bottomWallCapTexture = Content.Load<Texture2D> ("pipeBottomCap");

			font = Content.Load<SpriteFont> ("gameFont");

			groundBottom = Content.Load<Texture2D> ("bottomGround");
			ground.Initialize (Content, "ground", wallHeight, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, -GamePhysics.WallSpeed, false);
			clouds1.Initialize (Content, "clouds1", 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, -.25f, true);
			clouds2.Initialize (Content, "clouds2", 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, -(GamePhysics.WallSpeed + .5f), true);
			bushes.Initialize (Content, "bushes", wallHeight, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, -1, false, true);
			buildings.Initialize (Content, "buildings", wallHeight, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, -.5f, false, true);
		}

		public void Reset ()
		{
			wallSpanTime = 2000;
			player.Active = true;
			player.Health = 1;
			maxGap = 300;
			score = 0;
			walls.Clear ();

			var playerPosition = new Vector2 (graphics.GraphicsDevice.Viewport.Width / 3, graphics.GraphicsDevice.Viewport.Height / 2);
			player.Position = playerPosition;
		}

		#endregion

		#region Update and Draw

		/// <summary>
		/// Allows the game to run logic such as updating the world,
		/// checking for collisions, gathering input, and playing audio.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		protected override void Update (GameTime gameTime)
		{
			base.Update (gameTime);

			// Save the previous state of the keyboard and game pad so we can determinesingle key/button presses
			previousGamePadState = currentGamePadState;
			previousKeyboardState = currentKeyboardState;
			previousTouches = currentTouches;

			// Read the current state of the keyboard and gamepad and store it
			currentKeyboardState = Keyboard.GetState ();
			currentGamePadState = GamePad.GetState (PlayerIndex.One);
			currentTouches = TouchPanel.GetState ();


			//Update the player
			var shouldFly = Toggled (); //currentTouches.Any() || currentKeyboardState.IsKeyDown (Keys.Space) || currentGamePadState.IsButtonDown(Buttons.A) ;
			player.Update (gameTime, shouldFly, wallHeight + 1);


			if (!player.Active) {
				if (Toggled ())
					Reset ();
				return;
			}

			ground.Update ();
			buildings.Update ();
			bushes.Update ();
			clouds1.Update ();
			clouds2.Update ();

			UpdateWalls (gameTime);

			// Update the collision
			UpdateCollision ();

		}

		bool Toggled (Buttons button)
		{
			return previousGamePadState.IsButtonUp (button) && currentGamePadState.IsButtonDown (button);
		}

		bool ToggledTappped ()
		{
			return !previousTouches.Any () && currentTouches.Any ();
		}

		bool Toggled (Keys key)
		{
			return previousKeyboardState.IsKeyUp (key) && currentKeyboardState.IsKeyDown (key);
		}

		bool Toggled ()
		{
			return ToggledTappped () || Toggled (Buttons.A) || Toggled (Keys.Space);
		}

		int maxGap = 400;
		const int MaxWallheight = 920;

		private void AddWall ()
		{
			const int minGap = 214;
			maxGap = MathHelper.Clamp (maxGap - 10, minGap, 400);
			int gapSize = random.Next (minGap, maxGap);
			var gapY = random.Next (100, wallHeight - (gapSize + 100));
			var wall = new Wall ();

			var position = new Vector2 (GraphicsDevice.Viewport.Width + wallTexture.Width / 2, 0);
			// Initialize the animation with the correct animation information
			wall.Initialize (wallTexture, topWallCapTexture, bottomWallCapTexture, new Rectangle ((int)position.X, gapY, wallTexture.Width, gapSize), GraphicsDevice.Viewport.Height, GraphicsDevice.Viewport.Height);

			walls.Add (wall);
		}

		private void UpdateWalls (GameTime gameTime)
		{
			// Spawn a new enemy enemy every 1.5 seconds
			previousWallSpawnTime += gameTime.ElapsedGameTime.TotalMilliseconds;
			if (previousWallSpawnTime > wallSpanTime) {
				previousWallSpawnTime = 0;
				wallSpanTime -= 200;
				wallSpanTime = Math.Max (wallSpanTime, 2000);
				// Add an Enemy
				AddWall ();
			}
			var deadWalls = new List<Wall> ();
			foreach (var wall in walls) {
				wall.Update (gameTime);
				if (wall.Position.X < -wall.Width)
					deadWalls.Add (wall);
			}

			foreach (var wall in deadWalls)
				walls.Remove (wall);

		}

		private void UpdateCollision ()
		{
			// Use the Rectangle's built-in intersect function to 
			// determine if two objects are overlapping
			var rectangle1 = new Rectangle ((int)player.Position.X,
				                 (int)player.Position.Y,
				                 player.Width,
				                 player.Height);

			//If it collides with a wall, you die
			foreach (var wall in walls.Where(x=> x.Collides(rectangle1))) {
				player.Health = 0;
				player.Active = false;
			}

			var points = walls.Sum (x => x.CollectPoints ());
			score += points;

			if (rectangle1.Bottom >= wallHeight) {
				player.Health = 0;
				player.Active = false;
			}
		}

		/// <summary>
		/// This is called when the game should draw itself. 
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>

		protected override void Draw (GameTime gameTime)
		{
			GraphicsDevice.Clear (Color.CornflowerBlue);
			// Start drawing
			spriteBatch.Begin ();

			#if WINDOWS_PHONE
			spriteBatch.Draw(mainBackground, Vector2.Zero, Color.White);
			#endif

			#if MONOGAME
			spriteBatch.Draw(mainBackground, Vector2.Zero, null, Color.White, 0, Vector2.Zero, 1.3f, SpriteEffects.None, 0);
			#endif

			buildings.Draw (spriteBatch);
			bushes.Draw (spriteBatch);

			clouds1.Draw (spriteBatch);

			walls.ForEach (x => x.Draw (spriteBatch));


			// Draw the Player
			player.Draw (spriteBatch);

			clouds2.Draw (spriteBatch);

			spriteBatch.Draw (groundBottom, bottomGroundRect, Color.Wheat);
			ground.Draw (spriteBatch);


//			// Draw the score
			spriteBatch.DrawString (font, score.ToString (), new Vector2 (GraphicsDevice.Viewport.TitleSafeArea.Width / 2, GraphicsDevice.Viewport.TitleSafeArea.Height / 4), Color.White, 0, Vector2.Zero, 4f, SpriteEffects.None, 0);
//			// Draw the player health
//			spriteBatch.DrawString (font, "health: " + player.Health, new Vector2 (GraphicsDevice.Viewport.TitleSafeArea.X, GraphicsDevice.Viewport.TitleSafeArea.Y + 30), Color.White);

			// Stop drawing
			spriteBatch.End ();
			// TODO: Add your drawing code here

			base.Draw (gameTime);
		}

		#endregion
	}
}
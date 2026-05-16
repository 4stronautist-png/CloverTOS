using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Melia.Shared.Scripting;
using Melia.Zone.Events.Arguments;
using Melia.Zone.Network;
using Melia.Zone.World.Actors.Characters;
using Yggdrasil.Logging;

namespace Melia.Zone.Scripting
{
	/// <summary>
	/// Specialized script for sending Lua scripts to the client.
	/// </summary>
	public abstract class ClientScript : GeneralScript
	{
		public const int ScriptMaxLength = 2048;

		private readonly Dictionary<string, string> _files = new();
		private static readonly object _readyLock = new();
		private static readonly Dictionary<string, HashSet<string>> _readyScriptsBySession = new();
		private static readonly Dictionary<string, DateTime> _readySessionLastSeen = new();
		private static DateTime _lastReadySessionPrune = DateTime.MinValue;

		/// <summary>
		/// Initializes the script and explicitly subscribes the inherited
		/// PlayerReady handler. Reflection-based event loading doesn't
		/// reliably pick up inherited non-public handlers on derived
		/// ClientScript types, which would otherwise prevent the client
		/// Lua modules from being sent at all.
		/// </summary>
		/// <returns></returns>
		public override bool Init()
		{
			var result = base.Init();
			if (!result)
				return false;

			ZoneServer.Instance.ServerEvents.PlayerReady.Subscribe(this.OnPlayerReadyInternal);
			return true;
		}

		/// <summary>
		/// Unsubscribes explicit event handlers before the script is
		/// disposed.
		/// </summary>
		public override void Dispose()
		{
			ZoneServer.Instance.ServerEvents.PlayerReady.Unsubscribe(this.OnPlayerReadyInternal);
			base.Dispose();
		}

		/// <summary>
		/// Called to load the script files.
		/// </summary>
		protected override void Load()
		{
		}

		/// <summary>
		/// Called to send the scripts when the player is ready to receive them.
		/// </summary>
		/// <param name="character"></param>
		protected virtual void Ready(Character character)
		{
		}

		/// <summary>
		/// Called on map changes after the static Lua payload for this
		/// client session has already been installed.
		/// </summary>
		/// <param name="character"></param>
		protected virtual void ReadyAgain(Character character)
		{
		}

		/// <summary>
		/// Called when the player logs in and is ready to receive scripts.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		[On("PlayerReady")]
		protected void OnPlayerReadyInternal(object sender, PlayerEventArgs e)
		{
			if (!this.TryActivateClientScriptReady(e.Character))
			{
				this.ReadyAgain(e.Character);
				return;
			}

			this.Ready(e.Character);
		}

		private bool TryActivateClientScriptReady(Character character)
		{
			var sessionKey = character?.Connection?.SessionKey;
			if (string.IsNullOrWhiteSpace(sessionKey))
				return true;

			var scriptKey = this.GetType().AssemblyQualifiedName ?? this.GetType().FullName ?? this.GetType().Name;
			var now = DateTime.UtcNow;

			lock (_readyLock)
			{
				this.PruneReadySessionCache(now);

				_readySessionLastSeen[sessionKey] = now;

				if (!_readyScriptsBySession.TryGetValue(sessionKey, out var sentScripts))
				{
					sentScripts = new HashSet<string>();
					_readyScriptsBySession[sessionKey] = sentScripts;
				}

				return sentScripts.Add(scriptKey);
			}
		}

		private void PruneReadySessionCache(DateTime now)
		{
			if ((now - _lastReadySessionPrune).TotalMinutes < 15)
				return;

			_lastReadySessionPrune = now;

			var expiredKeys = _readySessionLastSeen
				.Where(pair => (now - pair.Value).TotalHours >= 6)
				.Select(pair => pair.Key)
				.ToList();

			foreach (var sessionKey in expiredKeys)
			{
				_readySessionLastSeen.Remove(sessionKey);
				_readyScriptsBySession.Remove(sessionKey);
			}
		}

		/// <summary>
		/// Adds script under the given name.
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="script"></param>
		private void AddLuaScript(string fileName, string script)
		{
			if (script.Length > ScriptMaxLength)
				Log.Warning("ClientScript: Script file '{0}', loaded by '{1}', exceeds the recommended maximum length of {2} characters. (Length: {3}).", fileName, this.GetType().Name, ScriptMaxLength, script.Length);

			_files[fileName] = script;
		}

		/// <summary>
		/// Loads a Lua script from the same directory as the script.
		/// </summary>
		/// <param name="fileName">File name of the script.</param>
		/// <param name="sourceFilePath">Please ignore. Used to determine the path to the script file.</param>
		protected void LoadLuaScript(string fileName, [CallerFilePath] string sourceFilePath = "")
		{
			var fileDirPath = Path.GetDirectoryName(sourceFilePath);
			var filePath = Path.Combine(fileDirPath, fileName);
			var script = File.ReadAllText(filePath);

			this.AddLuaScript(fileName, script);
		}

		/// <summary>
		/// Lodads the Lua code and remembers it under the given name.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="code"></param>
		protected void LoadLuaCode(string name, string code)
		{
			_files[name] = code;
		}

		/// <summary>
		/// Sends a previously loaded Lua script to the character's client.
		/// </summary>
		/// <param name="character"></param>
		/// <param name="fileName"></param>
		protected void SendLuaScript(Character character, string fileName)
		{
			var script = _files[fileName];
			Send.ZC_EXEC_CLIENT_SCP(character.Connection, script);
		}

		/// <summary>
		/// Sends a raw Lua script to the character's client.
		/// </summary>
		/// <param name="character"></param>
		/// <param name="script"></param>
		protected void SendRawLuaScript(Character character, string script)
		{
			if (script.Length > ScriptMaxLength)
				Log.Warning("ClientScript: Script '{0}', sent by '{1}', exceeds the recommended maximum length of {2} characters. (Length: {3}).", script, this.GetType().Name, ScriptMaxLength, script.Length);

			Send.ZC_EXEC_CLIENT_SCP(character.Connection, script);
		}

		/// <summary>
		/// Loads a Lua script from the same directory as the script.
		/// </summary>
		/// <param name="sourceFilePath">Please ignore. Used to determine the path to the script file.</param>
		protected void LoadAllScripts([CallerFilePath] string sourceFilePath = "")
		{
			var fileDirPath = Path.GetDirectoryName(sourceFilePath);
			var luaFilePaths = Directory.EnumerateFiles(fileDirPath, "*.lua").OrderBy(a => a);

			foreach (var filePath in luaFilePaths)
			{
				var fileName = Path.GetFileName(filePath);
				var script = File.ReadAllText(filePath);

				this.AddLuaScript(fileName, script);
			}
		}

		/// <summary>
		/// Returns the path to the script file that called this method.
		/// </summary>
		/// <param name="sourceFilePath"></param>
		/// <returns></returns>
		protected string GetCallingFilePath([CallerFilePath] string sourceFilePath = "")
			=> sourceFilePath;

		/// <summary>
		/// Sends all loaded Lua scripts to the character's client.
		/// </summary>
		/// <param name="character"></param>
		protected void SendAllScripts(Character character)
		{
			foreach (var file in _files)
			{
				var script = file.Value;
				Send.ZC_EXEC_CLIENT_SCP(character.Connection, script);
			}
		}
	}
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game
{
    public class PlayerData
    {
        // Core state
        public bool mRolling;
        public bool isAttacking;
        public int mFacingDirection;
        public int mCurrentAttack;
        public bool isBlocking; // NEW: track blocking
        public float mTimeSinceAttack;
        public float xPos;
        public float yPos;

        public bool rollingChanged;
        public bool attackingChanged;
        public bool blockingChanged; // track block state changes
        public uint id;

        public PlayerData()
        {
            mRolling = false;
            mFacingDirection = 1;
            mCurrentAttack = 1;
            xPos = 0;
            yPos = 0;
        }

        public void CopyData(PlayerData pd)
        {
            mRolling = pd.mRolling;
            isAttacking = pd.isAttacking;
            isBlocking = pd.isBlocking;
            mFacingDirection = pd.mFacingDirection;
            mCurrentAttack = pd.mCurrentAttack;
            xPos = pd.xPos;
            yPos = pd.yPos;
        }

        public void Add(PlayerData pd)
        {
            xPos += pd.xPos;
            yPos += pd.yPos;

            if (pd.rollingChanged) mRolling = pd.mRolling;
            if (pd.attackingChanged) isAttacking = pd.isAttacking;
            if (pd.blockingChanged) isBlocking = pd.isBlocking;
            if (pd.mFacingDirection != 0) mFacingDirection = pd.mFacingDirection;
        }

        public static PlayerData SubtractData(PlayerData newData, PlayerData oldData)
        {
            PlayerData delta = new PlayerData
            {
                rollingChanged = oldData.mRolling != newData.mRolling,
                attackingChanged = oldData.isAttacking != newData.isAttacking,
                blockingChanged = oldData.isBlocking != newData.isBlocking,
                mRolling = newData.mRolling,
                isAttacking = newData.isAttacking,
                isBlocking = newData.isBlocking,
                mFacingDirection = newData.mFacingDirection,
                mCurrentAttack = newData.mCurrentAttack - oldData.mCurrentAttack,
                xPos = newData.xPos - oldData.xPos,
                yPos = newData.yPos - oldData.yPos
            };

            return delta;
        }

        // Optimized decoder using dictionary
        private static readonly Dictionary<string, Action<PlayerData, string>> KeyActions = new()
        {
            ["Rolling"] = (pd, val) => { pd.mRolling = bool.Parse(val); pd.rollingChanged = true; },
            ["Atk"] = (pd, val) => { pd.isAttacking = bool.Parse(val); pd.attackingChanged = true; },
            ["Block"] = (pd, val) => { pd.isBlocking = bool.Parse(val); pd.blockingChanged = true; },
            ["Facing"] = (pd, val) => pd.mFacingDirection = int.Parse(val),
            ["X"] = (pd, val) => pd.xPos = float.Parse(val, CultureInfo.InvariantCulture),
            ["Y"] = (pd, val) => pd.yPos = float.Parse(val, CultureInfo.InvariantCulture)
        };

        public static PlayerData DeltaDataDecoder(string data)
        {
            PlayerData pd = new PlayerData();
            var pairs = data.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var pair in pairs)
            {
                var kv = pair.Split(':');
                if (kv.Length != 2) continue;

                if (KeyActions.TryGetValue(kv[0], out var action))
                    action(pd, kv[1]);
            }

            return pd;
        }

        public string ToDeltaString()
        {
            StringBuilder sb = new StringBuilder();

            if (rollingChanged) sb.Append($"Rolling:{mRolling},");
            if (attackingChanged) sb.Append($"Atk:{isAttacking},");
            if (blockingChanged) sb.Append($"Block:{isBlocking},");
            if (mFacingDirection != 0) sb.Append($"Facing:{mFacingDirection},");
            if (xPos != 0) sb.Append($"X:{xPos},");
            if (yPos != 0) sb.Append($"Y:{yPos},");

            if (sb.Length > 0) sb.Length--; // remove trailing comma
            return sb.ToString();
        }

        public string ToRealString()
        {
            return $"Rolling:{mRolling},Atk:{isAttacking},Block:{isBlocking},Facing:{mFacingDirection},X:{xPos},Y:{yPos}";
        }
        
        public static PlayerData[] ParseRealData(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new FormatException("PlayerData parsing failed: input string is null or empty.");

            string[] blocks = input.Split(';', StringSplitOptions.RemoveEmptyEntries);
            List<PlayerData> players = new List<PlayerData>();

            foreach (string block in blocks)
            {
                // Expect format: id|Rolling:...,Atk:...,Facing:...,X:...,Y:...
                string[] idAndData = block.Split('|');
                if (idAndData.Length != 2)
                    throw new FormatException($"PlayerData block '{block}' is missing or has multiple '|' separators.");

                PlayerData pd = new PlayerData();

                // Parse ID section
                if (!uint.TryParse(idAndData[0], out pd.id))
                    throw new FormatException($"Invalid player ID '{idAndData[0]}' in block '{block}'.");

                string[] pairs = idAndData[1].Split(',', StringSplitOptions.RemoveEmptyEntries);

                foreach (string pair in pairs)
                {
                    string[] kv = pair.Split(':');
                    if (kv.Length != 2)
                        throw new FormatException($"Malformed key-value pair '{pair}' inside block '{block}'.");

                    string key = kv[0];
                    string value = kv[1];

                    try
                    {
                        switch (key)
                        {
                            case "Rolling":
                                pd.mRolling = bool.Parse(value);
                                break;
                            case "Atk":
                                pd.isAttacking = bool.Parse(value);
                                break;
                            case "Block":
                                pd.isBlocking = bool.Parse(value);
                                break;
                            case "Facing":
                                pd.mFacingDirection = int.Parse(value);
                                break;
                            case "X":
                                pd.xPos = float.Parse(value, CultureInfo.InvariantCulture);
                                break;
                            case "Y":
                                pd.yPos = float.Parse(value, CultureInfo.InvariantCulture);
                                break;
                            default:
                                throw new FormatException($"Unknown key '{key}' in block '{block}'.");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new FormatException(
                            $"Invalid value '{value}' for key '{key}' in block '{block}'.", ex);
                    }
                }

                players.Add(pd);
            }

            return players.ToArray();
        }
    }
}
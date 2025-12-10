using System;
using System.Collections.Generic;
using System.Text;

namespace Game
{
    public class PlayerData
    {
        public bool mRolling;
        public bool isAttacking;
        public int mFacingDirection;
        public int mCurrentAttack;
        public float mTimeSinceAttack; //todo: implement
        public float xPos;
        public float yPos;

        public bool rollingChanged;
        public bool attackingChanged;
        public uint id;

        public PlayerData()
        {
            mRolling = false;
            mFacingDirection = 1;
            mCurrentAttack = 1;
            xPos = 0;
            yPos = 0;
        }

        public void CopyData(PlayerData playerData)
        {
            mRolling = playerData.mRolling;
            mFacingDirection = playerData.mFacingDirection;
            mCurrentAttack = playerData.mCurrentAttack;
            xPos = playerData.xPos;
            yPos = playerData.yPos;
        }

        public void Add(PlayerData playerData)
        {
            xPos += playerData.xPos;
            yPos += playerData.yPos;
            
            if (playerData.rollingChanged) mRolling = playerData.mRolling;
            if (playerData.attackingChanged) isAttacking = playerData.isAttacking;
            if (playerData.mFacingDirection != 0) mFacingDirection = playerData.mFacingDirection;
        }
        
        public static PlayerData SubtractData(PlayerData newData, PlayerData oldData)
        {
            PlayerData deltaData = new PlayerData();

            deltaData.rollingChanged = oldData.mRolling != newData.mRolling;
            deltaData.attackingChanged = oldData.isAttacking != newData.isAttacking;
            
            if (oldData.mFacingDirection != newData.mFacingDirection) 
                deltaData.mFacingDirection = 0;
            else deltaData.mFacingDirection = newData.mFacingDirection;
            
            deltaData.mRolling = newData.mRolling;
            deltaData.mFacingDirection = newData.mFacingDirection;
            deltaData.isAttacking = newData.isAttacking;
            
            deltaData.mCurrentAttack = newData.mCurrentAttack - oldData.mCurrentAttack;
            deltaData.xPos = newData.xPos - oldData.xPos;
            deltaData.yPos = newData.yPos - oldData.yPos;
            
            return deltaData;
        }

        public static PlayerData DeltaDataDecoder(string data)
        {
            PlayerData newData = new PlayerData();
            
            // Split the string into key:value pairs
            string[] pairs = data.Split(',');

            foreach (string pair in pairs)
            {
                string[] kv = pair.Split(':');
                if (kv.Length != 2) continue;

                string key = kv[0];
                string value = kv[1];

                switch (key)
                {
                    case "Rolling":
                        newData.mRolling = bool.Parse(value);
                        newData.rollingChanged = true;
                        break;
                    case "Facing":
                        newData.mFacingDirection = int.Parse(value);
                        break;
                    case "Atk":
                        newData.isAttacking = bool.Parse(value);
                        newData.attackingChanged = true;
                        break;
                    case "X":
                        newData.xPos = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                        break;
                    case "Y":
                        newData.yPos = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                        break;
                }
            }

            return newData;
        }

        /*
         * Form:
         * Rolling:{boolean},
         * Atk:{boolean},
         * Facing:{int},
         * X:{float},
         * Y:{float}
         *
         * The line breaks are purely visual and are not present within the packet
         */
        public string ToDeltaString()
        {
            StringBuilder sb = new StringBuilder();

            if (rollingChanged)
                sb.Append($"Rolling:{mRolling},");
            if (attackingChanged)
                sb.Append($"Atk:{isAttacking},");
            if (mFacingDirection != 0)
                sb.Append($"Facing:{mFacingDirection},");
            if (xPos != 0)
                sb.Append($"X:{xPos},");
            if (yPos != 0)
                sb.Append($"Y:{yPos},");
            
            // Remove the trailing comma if present
            if (sb.Length > 0)
                sb.Length--;
            
            return sb.ToString();
        }

        public string ToRealString()
        {
            StringBuilder sb = new StringBuilder();
            
            sb.Append($"Rolling:{mRolling},");
            sb.Append($"Atk:{isAttacking},");
            sb.Append($"Facing:{mFacingDirection},");
            sb.Append($"X:{xPos},");
            sb.Append($"Y:{yPos}");
            
            return sb.ToString();
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

                            case "Facing":
                                pd.mFacingDirection = int.Parse(value);
                                break;

                            case "X":
                                pd.xPos = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                                break;

                            case "Y":
                                pd.yPos = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
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
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
    }
}
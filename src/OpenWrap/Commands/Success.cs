﻿using System;

namespace OpenWrap.Commands
{
    public class Success : ICommandOutput
    {
        public Success()
        {
            Type = CommandResultType.Info;
        }

        public CommandResultType Type { get; protected set; }

        public override string ToString()
        {
            return "The command executed successfully.";
        }
    }
}
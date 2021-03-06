﻿using NUnit.Framework;
using OpenWrap.Tests.Build.build_instruction_emitter_specs.contexts;

namespace OpenWrap.Build.build_instruction_emitter
{
    public class doc_for_unreferenced_assembly : msbuild_emitter
    {
        public doc_for_unreferenced_assembly()
        {
            given_export_name("bin-net35");

            given_documentation_file("sauron.xml");

            when_generating_instructions();
        }

        [Test]
        public void included()
        {
            should_not_have_file("bin-net35", "sauron.xml");
        }
    }
}
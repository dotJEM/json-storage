using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotJEM.Json.Storage.Util;
using NUnit.Framework;

namespace DotJEM.Json.Storage.Test.Util
{
	[TestFixture]
    public class AdvPropertyBagTest
    {
        [TestCase("See @(caracter) in @(movie).", "See Peter Parker in Spider Man.")]
        [TestCase("@(movie) was produced in @(year).", "Spider Man was produced in 2002.")]
	    public void FormatUsingStandard(string format, string expected)
		{
		    var bag = new AdvPropertyBag();
            bag.Add("id", 1);
            bag.Add("stage", 5);
            bag.Add("caracter", "Peter Parker");
            bag.Add("movie", "Spider Man");
            bag.Add("year", 2002);

			Assert.That(bag.Format(format), Is.EqualTo(expected));
		}

        [TestCase("See <!--caracter--> in <!--movie-->.", "See Peter Parker in Spider Man.")]
        [TestCase("<!--movie--> was produced in <!--year-->.", "Spider Man was produced in 2002.")]
        public void FormatUsingCustomLong(string format, string expected)
        {
            var bag = new AdvPropertyBag("<!--", "-->");
            bag.Add("id", 1);
            bag.Add("stage", 5);
            bag.Add("caracter", "Peter Parker");
            bag.Add("movie", "Spider Man");
            bag.Add("year", 2002);

            Assert.That(bag.Format(format), Is.EqualTo(expected));
        }

        [TestCase("See {caracter} in {movie}.", "See Peter Parker in Spider Man.")]
        [TestCase("{movie} was produced in {year}.", "Spider Man was produced in 2002.")]
        public void FormatUsingCustomShort(string format, string expected)
        {
            var bag = new AdvPropertyBag("{", "}");
            bag.Add("id", 1);
            bag.Add("stage", 5);
            bag.Add("caracter", "Peter Parker");
            bag.Add("movie", "Spider Man");
            bag.Add("year", 2002);

            Assert.That(bag.Format(format), Is.EqualTo(expected));
        }

    }
}

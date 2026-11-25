// The Stats page's entry point (stats.html, #265)
import "./site";
import { setUpStats } from "./stats/page";

const root = document.getElementById("stats");
if (root) void setUpStats(root);

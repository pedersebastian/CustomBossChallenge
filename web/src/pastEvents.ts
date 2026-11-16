// The Past Events page's entry point (past-events.html, #213)
import "./site";
import { setUpPastEvents } from "./pastEvents/list";

const root = document.getElementById("past-events");
if (root) void setUpPastEvents(root);

using X20Ctl.Zone.Input;
namespace X20Ctl.Zone.Tests;
public class InputOwnershipTests
{
 [Fact] public void Assignment_is_explicit_exclusive_and_never_falls_back()
 {
  var a=new PlayerInputAssignments(); Assert.Null(a.Source(0));
  Assert.True(a.Select(0,"pad-a")); Assert.False(a.Select(1,"pad-a"));
  Assert.True(a.Select(1,"pad-b")); a.RemoveMissing(["pad-b","pad-c"]);
  Assert.Null(a.Source(0)); Assert.Equal("pad-b",a.Source(1));
  Assert.True(a.Select(0,"pad-a")); Assert.Equal("pad-a",a.Source(0));
 }
 [Fact] public void Stale_disconnect_cannot_clear_new_selection_or_reselected_same_source()
 {
  var a=new PlayerInputAssignments(); a.Select(0,"a");long old=a.Revision(0);
  a.Select(0,"b");Assert.False(a.ClearIfCurrent(0,"a",old));Assert.Equal("b",a.Source(0));
  a.Select(0,"a");long selected=a.Revision(0);a.Select(0,"a");
  Assert.False(a.ClearIfCurrent(0,"a",selected));Assert.True(a.ClearIfCurrent(0,"a",a.Revision(0)));Assert.Null(a.Source(0));
 }
 [Fact] public void Neutral_barrier_does_not_accept_activation_or_held_buttons()
 {
  var c=new ButtonCapture(); c.Begin(); c.Feed(["A"]); Assert.Null(c.Source);
  c.Feed([]); c.Feed(["LB"]); Assert.Equal("LB",c.Source); Assert.Null(c.Target);
  c.Feed(["LB","B"]); Assert.Null(c.Target); c.Feed([]); c.Feed(["B"]);
  Assert.Equal("B",c.Target); Assert.False(c.Active);
 }
 [Fact] public void Simultaneous_source_or_target_does_not_pick_enumeration_order()
 {
  var c=new ButtonCapture(); c.Begin(); c.Feed([]); c.Feed(["A","B"]); Assert.Null(c.Source);
  c.Feed([]); c.Feed(["RB"]); c.Feed([]); c.Feed(["X","Y"]); Assert.Null(c.Target);
  c.Feed([]); c.Feed(["X"]); Assert.Equal("X",c.Target);
 }
 [Fact] public void Cancel_clears_incomplete_capture_without_a_target()
 {
  var c=new ButtonCapture(); c.Begin("A"); c.Feed([]); c.Cancel(); c.Feed(["B"]);
  Assert.False(c.Active); Assert.Null(c.Target);
 }
 [Fact] public void Navigation_requires_neutral_and_resets_on_owner_change()
 {
  var g=new NavigationGestures(); Assert.Empty(g.Read(["LB"],0));
  Assert.Empty(g.Read([],1)); Assert.Equal(new[]{"LB"},g.Read(["LB"],2));
  Assert.Empty(g.Read(["LB"],1000)); g.Reset(); Assert.Empty(g.Read(["A"],1001));
  g.Read([],1002); Assert.Equal(new[]{"A"},g.Read(["A"],1003));
 }
 [Fact] public void Only_directions_repeat_and_conflicting_chords_do_not_navigate()
 {
  var g=new NavigationGestures();g.Read([],0);
  Assert.Equal(new[]{"DPAD_LEFT"},g.Read(["DPAD_LEFT"],10));
  Assert.Empty(g.Read(["DPAD_LEFT"],100)); Assert.Equal(new[]{"DPAD_LEFT"},g.Read(["DPAD_LEFT"],410));
  Assert.Empty(g.Read(["DPAD_LEFT"],420)); g.Read([],600);
  Assert.Empty(g.Read(["LB","RB"],601)); Assert.Empty(g.Read(["A","B"],602));
 }
 [Theory] [InlineData(false,false,false,false)] [InlineData(true,true,false,false)] [InlineData(true,false,true,false)] [InlineData(true,false,false,true)]
 public void Navigation_is_suppressed_outside_its_owner(bool active,bool text,bool exclusive,bool mouse)
 { Assert.False(InputOwnership.CanNavigate(active,text,exclusive,mouse)); }
 [Fact] public void Stick_navigation_has_hysteresis_and_one_dominant_direction()
 {
  var s=new StickNavigation();Assert.Null(s.Read(.59,0));Assert.Equal("DPAD_RIGHT",s.Read(.7,.5));
  Assert.Equal("DPAD_RIGHT",s.Read(.5,0));Assert.Null(s.Read(.39,0));Assert.Equal("DPAD_UP",s.Read(.3,-.8));
  s.Reset();Assert.Null(s.Read(0,0));
 }
 [Fact] public void Plain_browse_is_navigable()=>Assert.True(InputOwnership.CanNavigate(true,false,false,false));
}

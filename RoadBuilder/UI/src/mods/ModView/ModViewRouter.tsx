import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { RoadBuilderToolModeEnum } from "domain/RoadBuilderToolMode";
import { roadBuilderToolMode$ } from "mods/bindings";
import { BottomView } from "mods/BottomView/BottomView";
import ActionPopup from "mods/Components/ActionPopup/ActionPopup";
import { RoadPropertiesPanel } from "mods/RoadPropertiesPanel/RoadPropertiesPanel";
import { SidePanel } from "mods/SidePanel/SidePanel";
import styles from "./ModView.module.scss";

import RB_ClickOnRoad from "images/RB_ClickOnRoad.svg";

export const Router = () => {
  const roadBuilderToolMode = useValue(roadBuilderToolMode$);
  let { translate } = useLocalization();

  let content: JSX.Element | null = null;
  switch (roadBuilderToolMode) {
    case RoadBuilderToolModeEnum.Picker:
      content = (
        <>
          <div className={styles.pickerHint}>
            <span>
              <img src={RB_ClickOnRoad} />
              {translate("Prompt[PickerHint]", "Click On A Road")}
            </span>
          </div>
          <SidePanel />
        </>
      );
      break;
    case RoadBuilderToolModeEnum.ActionSelection:
      content = (
        <>
          <ActionPopup />
        </>
      );
      break;
    case RoadBuilderToolModeEnum.Editing:
    case RoadBuilderToolModeEnum.EditingSingle:
    case RoadBuilderToolModeEnum.EditingNonExistent:
      content = (
        <>
          <SidePanel />
          <BottomView />
          <RoadPropertiesPanel />
        </>
      );
      break;
    default:
      return <></>;
  }    

  return (
    <div className={styles.view}>{content}</div>
  )
}
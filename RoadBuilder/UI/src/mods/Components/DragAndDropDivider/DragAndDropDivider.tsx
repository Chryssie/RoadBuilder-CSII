import { useContext } from 'react';
import styles from './DragAndDropDivider.module.scss';
import { DragContext } from 'mods/Contexts/DragContext';
import classNames from 'classnames';

export const DragAndDropDivider = () => {
    let dragContext = useContext(DragContext);    
    let containerClasses = classNames(
        styles.container,
        {
            [styles.hidden]: dragContext.netSectionItem === undefined
        }
    );

    let x = 'fdas';
    let y = {x: 32};

    return (
        <div className={containerClasses}>
            <div className={styles.divider}>
            </div>
        </div>        
    )
}
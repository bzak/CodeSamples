import * as React from "react"
import { t, thtml } from "../i18n/translate"
import { connectedForm } from "../Commons/forms"
import { LicenseInfo } from "../Api/api"

const boundState = (state) => state.auth;
const boundProps = ['license'];
const boundActions = {};

interface ExpiredProps {
    location: HistoryModule.Location;
    license: LicenseInfo;
}
export default connectedForm(boundState, boundProps, boundActions)(
    class Expired extends React.Component<ExpiredProps, {}> {
        render() {
            return (
                <div>
                    <h1 className="login-title">                    
                        {t("Auth:Your license has expired")}.<br /><br />
                        {t("Auth:Please contact your admin to restore access to this application")}.
                    </h1>
                    {this.props.license.adminName &&
                        <p>{this.props.license.adminName}</p>
                    }
                    {this.props.license.adminEmail &&
                        <p><a href={"mailto:" + this.props.license.adminEmail}>{this.props.license.adminEmail}</a></p>
                    }
                </div>
            );
        }
    }
)

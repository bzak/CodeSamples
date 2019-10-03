import * as React from "react"
import { Link } from "react-router"
import { t } from "../i18n/translate"

export default class DemoSubmitted extends React.Component<{}, {}> {
    render() {
        return (
            <div>
                <h1 className="login-title">
                    {t("Auth:Thank you!")}
                </h1>
                <p>{t("Auth:Our consultant will set up a demo")}</p>
                <Link to="/auth/demo" className="btn btn-default btn-lg " style={{ marginTop: 20 }}>{t("Auth:Resubmit form")}</Link>
            </div>
        );
    }
}
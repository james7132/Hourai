use crate::Route;
use yew::prelude::*;
use yew_router::prelude::*;
use ybc::*;

fn navbar_start() -> Html {
    html! {
        <>
            <NavbarItem tag={NavbarItemTag::A} href={Some("https://discord.gg/UydKWHX")}>
                { "Support Discord" }
            </NavbarItem>
            <NavbarItem tag={NavbarItemTag::A} href={Some("https://github.com/james7132/Hourai")}>
                { "GitHub" }
            </NavbarItem>
            <NavbarItem tag={NavbarItemTag::A} href={Some("https://twitter.com/hourai_gg")}>
                { "Twitter" }
            </NavbarItem>
        </>
    }
}

fn navbar_end() -> Html {
    html! {
        <>
            <NavbarItem tag={NavbarItemTag::A} href={Some("https://docs.hourai.gg")}>
                { "Documentation" }
            </NavbarItem>
            <NavbarItem tag={NavbarItemTag::A} href={Some("https://status.hourai.gg")}>
                { "Status" }
            </NavbarItem>
            <Link<Route>
                classes={classes!("navbar-item")}
                to={Route::Dashboard}>
                { "Dashboard" }
            </Link<Route>>
        </>
    }
}

#[function_component(LandingNavbar)]
fn navbar() -> Html {
    html! {
        <Container>
            <Navbar
                navstart={navbar_start()}
                navend={navbar_end()}
            />
        </Container>
    }
}

#[function_component(LandingPage)]
pub fn landing_page() -> Html {
    let hero_head = html! { <LandingNavbar/> };
    let hero_body = html! { "hi" };
    html! {
        <Hero
            classes={classes!("is-fullheight")}
            head={hero_head}
            body={hero_body}
        />
    }
}
